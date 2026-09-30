using System.IO.Ports;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class GatewayService(ILogger<GatewayService> logger, IDispatcher dispatcher, IHmiOutboundChannel hmiOutboundChannel) : BackgroundService
{
  const int BAUD_RATE = 9600;
  const int DEFAULT_READ_DELAY = 1000;
  const int GATEWAY_INFO_READ_TIMEOUT = 2000;
  const int GATEWAY_BOOT_DELAY = 5000;
  const int MAX_RETRIES = 5;
  const int MAC_SIZE = 6;
  const int PAYLOAD_SIZE = 16;
  const int PACKET_SIZE = MAC_SIZE + PAYLOAD_SIZE;

  // Main-loop reads must not block indefinitely, or a quiet Node/panel would starve outbound Hmi writes.
  const int MAIN_PORT_READ_TIMEOUT = 250;
  static readonly byte[] PACKET_HEADER = [0xAA, 0x55];
  static readonly byte[] GATEWAY_INFO_HEADER = [0xAB, 0x56];

  // Device (Hmi) messages are variable length and framed as [header][6 byte MAC][2 byte LE length][payload].
  // Distinct from PACKET_HEADER so Node's fixed-size sensor_payload frames (never carrying a header of their
  // own - Gateway.ino only prepends PACKET_HEADER for those) keep working unchanged.
  static readonly byte[] DEVICE_UPLINK_HEADER = [0xAC, 0x57];
  static readonly byte[] DEVICE_DOWNLINK_HEADER = [0xAD, 0x58];
  static readonly IReadOnlyList<byte[]> KNOWN_INCOMING_HEADERS = [PACKET_HEADER, DEVICE_UPLINK_HEADER];

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
          port ??= new SerialPort(portName, BAUD_RATE);
        }

        if (!port.IsOpen)
        {
          port.Open();
          port.ReadTimeout = MAIN_PORT_READ_TIMEOUT;
          await Task.Delay(GATEWAY_BOOT_DELAY, stoppingToken);
        }

        DrainOutboundHmiMessages(port);

        var header = TryReadIncomingHeader(port, KNOWN_INCOMING_HEADERS, stoppingToken);
        if (header is null)
        {
          retryCount = 0;
          continue;
        }

        if (header.SequenceEqual(PACKET_HEADER))
        {
          var packet = ReadPacket(port);
          if (packet != null)
          {
            var result = await dispatcher.DispatchAsync(new SaveSensorPacketRequest(packet), stoppingToken);
            result.EnsureSuccess();
          }

          await Task.Delay(DEFAULT_READ_DELAY, stoppingToken);
        }
        else
        {
          await HandleDeviceUplinkMessage(port, stoppingToken);
        }

        retryCount = 0;
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
        port = new SerialPort(portName, BAUD_RATE, Parity.None, 8, StopBits.One) { ReadTimeout = GATEWAY_INFO_READ_TIMEOUT };
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

  // Generalized header sniff for the main loop, which - unlike the gateway info handshake - must recognize
  // more than one possible frame type (sensor packets vs relayed Hmi device messages) on the same stream.
  private static byte[]? TryReadIncomingHeader(SerialPort port, IReadOnlyList<byte[]> headers, CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        int first = port.ReadByte();
        if (first < 0)
          return null;

        var candidates = headers.Where(header => header[0] == first).ToList();
        if (candidates.Count == 0)
          continue;

        int second = port.ReadByte();
        if (second < 0)
          return null;

        var match = candidates.FirstOrDefault(header => header[1] == second);
        if (match is not null)
          return match;
      }
    }
    catch (TimeoutException)
    {
      return null;
    }

    return null;
  }

  private async Task HandleDeviceUplinkMessage(SerialPort port, CancellationToken stoppingToken)
  {
    var macBytes = ReadBytes(port, MAC_SIZE);
    var lengthBytes = macBytes is null ? null : ReadBytes(port, 2);
    if (macBytes is null || lengthBytes is null)
      return;

    var length = BitConverter.ToUInt16(lengthBytes, 0);
    var payload = length == 0 ? [] : ReadBytes(port, length);
    if (payload is null)
      return;

    var macAddress = string.Join(':', macBytes.Select(b => b.ToString("X2"))).ToLowerInvariant();
    var result = await dispatcher.DispatchAsync(new ReceiveHmiCommandRequest(macAddress, payload), stoppingToken);
    result.EnsureSuccess();
  }

  // GatewayService is the sole owner/writer of the serial port, so outbound frames are drained here rather
  // than written directly by whichever handler produced them (e.g. BroadcastHmiStateHandler).
  private void DrainOutboundHmiMessages(SerialPort port)
  {
    while (hmiOutboundChannel.TryRead(out var message))
    {
      try
      {
        WriteDeviceDownlinkFrame(port, message.MacAddress, message.Payload);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to relay an outbound Hmi message to {MacAddress}.", message.MacAddress);
        break;
      }
    }
  }

  private static void WriteDeviceDownlinkFrame(SerialPort port, string macAddress, byte[] payload)
  {
    var macBytes = macAddress.Split(':').Select(part => Convert.ToByte(part, 16)).ToArray();
    var lengthBytes = BitConverter.GetBytes((ushort)payload.Length);

    port.Write(DEVICE_DOWNLINK_HEADER, 0, DEVICE_DOWNLINK_HEADER.Length);
    port.Write(macBytes, 0, macBytes.Length);
    port.Write(lengthBytes, 0, lengthBytes.Length);
    port.Write(payload, 0, payload.Length);
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

    try
    {
      while (read < count)
      {
        int n = port.Read(buffer, read, count - read);
        if (n <= 0)
          return null;

        read += n;
      }
    }
    catch (TimeoutException)
    {
      return null;
    }

    return buffer;
  }
}
