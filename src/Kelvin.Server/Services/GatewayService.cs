using System.IO.Ports;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class GatewayService(ILogger<GatewayService> logger, IDispatcher dispatcher) : BackgroundService
{
  const int BAUD_RATE = 9600;
  const int DEFAULT_READ_DELAY = 1000;
  const int GATEWAY_INFO_READ_TIMEOUT = 2000;
  const int GATEWAY_BOOT_DELAY = 3000;
  const int MAX_RETRIES = 5;
  const int MAC_SIZE = 6;
  const int PAYLOAD_SIZE = 16;
  const int PACKET_SIZE = MAC_SIZE + PAYLOAD_SIZE;
  static readonly byte[] PACKET_HEADER = [0xAA, 0x55];
  static readonly byte[] GATEWAY_INFO_HEADER = [0xAB, 0x56];

  /*
  Error cases to solve for:
    Connection to the gateway unable to reconnect after a period of time
    Connected to the gateway, but hasn't received any data in a period of time
      - Send a notification to the client
      - Revert control to the dumb thermostat
  */

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var retryCount = 0;

    SerialPort? port = null;

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        if (port is null)
        {
          var portName = await FindGateway(stoppingToken);
          //port ??= new SerialPort(portName, BAUD_RATE, Parity.None, 8, StopBits.One) { ReadTimeout = DEFAULT_READ_DELAY };
          port ??= new SerialPort(portName, BAUD_RATE);
        }

        if (!port.IsOpen)
        {
          port.Open();
        }

        if (!ReadHeader(port, PACKET_HEADER, stoppingToken))
        {
          retryCount = 0;
          continue;
        }

        var packet = ReadPacket(port);
        if (packet != null)
        {
          var result = await dispatcher.DispatchAsync(new SaveSensorPacketRequest(packet), stoppingToken);
          result.EnsureSuccess();
        }

        retryCount = 0;
        await Task.Delay(DEFAULT_READ_DELAY, stoppingToken);
      }
      catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ObjectDisposedException)
      {
        retryCount++;

        if (retryCount >= MAX_RETRIES)
        {
          // The backoff was attempting to reconnect to the same port
          // if the port can't reconnect, it could be that the port changed.
          // clear the port and the retries and let it try to find the gateway on a different port.
          logger.LogWarning("Gateway port closed after {MAX_RETRIES} retries.", MAX_RETRIES);
          logger.LogInformation("Attempting to find Gateway on another port.");

          port?.Close();
          port?.Dispose();
          port = null;

          retryCount = 0;
          continue;
        }

        var backoffMs = DEFAULT_READ_DELAY * retryCount;
        logger.LogWarning(ex, "Gateway port error. Retrying in {backoffMs}ms ({retryCount}/{MAX_RETRIES})", backoffMs, retryCount, MAX_RETRIES);

        await Task.Delay(backoffMs, stoppingToken);

        try
        {
          if (port?.IsOpen == true)
          {
            port?.Close();
          }

          port?.Open();
        }
        catch (Exception e)
        {
          logger.LogError(e, "Failed to reopen port.");
        }
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("GatewayService is stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred in GatewayService while ingesting sensor packets.");
      }
    }

    port?.Close();
    port?.Dispose();
  }

  private async Task<string> FindGateway(CancellationToken stoppingToken)
  {
    var availablePorts = SerialPort.GetPortNames();
    foreach (var portName in availablePorts)
    {
      SerialPort? port = null;
      try
      {
        port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One) { ReadTimeout = GATEWAY_INFO_READ_TIMEOUT };
        port.Open();

        // Opening the port toggles DTR, which resets the ESP32; give it time to finish setup() before probing.
        await Task.Delay(GATEWAY_BOOT_DELAY, stoppingToken);

        port.WriteLine("info");

        if (TryReadGatewayMacResponse(port, out var macAddress))
        {
          var result = await dispatcher.DispatchAsync(new SaveGatewayMacAddressRequest(macAddress), stoppingToken);
          result.EnsureSuccess();

          return portName;
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred when attempting to connect to {portName}", portName);
      }
      finally
      {
        port?.Close();
        port?.Dispose();
        port = null;
      }
    }

    throw new InvalidOperationException("No valid gateway port found");
  }

  private static bool TryReadGatewayMacResponse(SerialPort port, out string macAddress)
  {
    macAddress = string.Empty;

    try
    {
      if (!ReadHeader(port, GATEWAY_INFO_HEADER, CancellationToken.None))
        return false;

      var macBytes = ReadBytes(port, MAC_SIZE);
      if (macBytes is null)
        return false;

      macAddress = string.Join(':', macBytes.Select(b => b.ToString("X2"))).ToLowerInvariant();
      return true;
    }
    catch (TimeoutException)
    {
      return false;
    }
  }

  private static bool ReadHeader(SerialPort port, byte[] header, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      int first = port.ReadByte();
      if (first < 0)
        return false;

      if (first != header[0])
        continue;

      int second = port.ReadByte();
      if (second < 0)
        return false;

      if (second == header[1])
        return true;
    }

    return false;
  }

  private static SensorPacket? ReadPacket(SerialPort port)
  {
    var buffer = ReadBytes(port, PACKET_SIZE);
    if (buffer is null)
      return null;

    var macBytes = buffer[..MAC_SIZE];
    var packet = new SensorPacket
    {
      MacAddress = string.Join(':', macBytes.Select(b => b.ToString("X2"))).ToLowerInvariant(),
      TemperatureC = BitConverter.ToSingle(buffer, MAC_SIZE + 0),
      HumidityPercentage = BitConverter.ToSingle(buffer, MAC_SIZE + 4),
      CO2LevelPpm = BitConverter.ToUInt16(buffer, MAC_SIZE + 8),
      BatteryLevelPercentage = BitConverter.ToSingle(buffer, MAC_SIZE + 12),
    };

    return packet;
  }

  private static byte[]? ReadBytes(SerialPort port, int count)
  {
    var buffer = new byte[count];
    int read = 0;

    while (read < count)
    {
      int n = port.Read(buffer, read, count - read);
      if (n <= 0)
        return null;

      read += n;
    }

    return buffer;
  }
}
