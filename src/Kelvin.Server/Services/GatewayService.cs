using System.IO.Ports;
using Kelvin.Server.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Sensors;

namespace Kelvin.Server.Services;

public class GatewayService(ILogger<GatewayService> logger, IServiceProvider serviceProvider) : BackgroundService
{
  const int DEFAULT_READ_DELAY = 1000;
  const int MAX_RETRIES = 5;
  const int MAC_SIZE = 6;
  const int PAYLOAD_SIZE = 16;
  const int PACKET_SIZE = MAC_SIZE + PAYLOAD_SIZE;
  static readonly byte[] HEADER = [0xAA, 0x55];

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
          port ??= new SerialPort(portName);
        }

        if (!port.IsOpen)
        {
          port.Open();
        }

        if (!ReadHeader(port, stoppingToken))
        {
          retryCount = 0;
          continue;
        }

        var packet = ReadPacket(port);
        if (packet != null)
        {
          Console.WriteLine(packet);
          using var scope = serviceProvider.CreateScope();
          var sensorsManager = scope.ServiceProvider.GetRequiredService<ISensorsManager>();
          await sensorsManager.SaveSensorPacket(packet, stoppingToken);
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
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
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
        port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
        port.Open();
        port.WriteLine("info");

        var response = port.ReadLine();
        if (response.Contains("Gateway MAC: "))
        {
          var macAddress = response.Replace("Gateway MAC: ", string.Empty);
          using var scope = serviceProvider.CreateScope();
          var gatewayManager = scope.ServiceProvider.GetRequiredService<IGatewayManager>();

          await gatewayManager.UpdateGatewayMacAddress(macAddress, stoppingToken);

          return portName;
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An occurred when attempting to connect to {portName}", portName);
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

  private static bool ReadHeader(SerialPort port, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      int first = port.ReadByte();
      if (first < 0)
        return false;

      if (first != HEADER[0])
        continue;

      int second = port.ReadByte();
      if (second < 0)
        return false;

      if (second == HEADER[1])
        return true;
    }

    return false;
  }

  private static SensorPacket? ReadPacket(SerialPort port)
  {
    var buffer = new byte[PACKET_SIZE];
    int read = 0;

    while (read < PACKET_SIZE)
    {
      int n = port.Read(buffer, read, PACKET_SIZE - read);
      if (n <= 0)
        return null;
      read += n;
    }

    var macBytes = buffer[..MAC_SIZE];
    var packet = new SensorPacket
    {
      MacAddress = string.Join(':', macBytes.Select(b => b.ToString("X2"))).ToLowerInvariant(),
      Temperature = BitConverter.ToSingle(buffer, MAC_SIZE + 0),
      Humidity = BitConverter.ToSingle(buffer, MAC_SIZE + 4),
      CO2Level = BitConverter.ToUInt16(buffer, MAC_SIZE + 8),
      BatteryLevel = BitConverter.ToSingle(buffer, MAC_SIZE + 12),
    };

    return packet;
  }
}
