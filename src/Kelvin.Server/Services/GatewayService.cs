using System.IO.Ports;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class GatewayService : BackgroundService
{
  const int MAX_RETRIES = 5;
  const int MAC_SIZE = 6;
  const int PAYLOAD_SIZE = 16;
  const int PACKET_SIZE = MAC_SIZE + PAYLOAD_SIZE;
  static readonly byte[] HEADER = [0xAA, 0x55];

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var retryCount = 0;

    using var port = FindGatewayPort();
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
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
        }

        retryCount = 0;
        await Task.Delay(1000, stoppingToken);
      }
      catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ObjectDisposedException)
      {
        retryCount++;

        if (retryCount >= MAX_RETRIES)
        {
          Console.WriteLine($"Gateway port closed after {MAX_RETRIES} retries. Giving up.");
          break;
        }

        var backoffMs = 250 * retryCount;
        Console.WriteLine($"Gateway port error: {ex.Message}. Retrying in {backoffMs}ms ({retryCount}/{MAX_RETRIES})");

        await Task.Delay(backoffMs, stoppingToken);

        try
        {
          if (port.IsOpen)
          {
            port.Close();
          }

          port.Open();
        }
        catch
        {
          // Ignore and continue retrying
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }
  }

  static SerialPort FindGatewayPort()
  {
    var availablePorts = SerialPort.GetPortNames();
    foreach (var portName in availablePorts)
    {
      try
      {
        var port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
        port.Open();
        port.WriteLine("info");

        var response = port.ReadLine();
        if (response.Contains("Gateway MAC: "))
        {
          // TODO: store/update the device if needed
          Console.WriteLine($"Found gateway on port {portName}, response: {response}");
          return port;
        }

        port.Close();
        port.Dispose();
        port = null;
      }
      catch
      {
        // Ignore and continue searching
      }
    }

    throw new InvalidOperationException("No valid gateway port found");
  }

  static bool ReadHeader(SerialPort port, CancellationToken cancellationToken)
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

  static SensorPacket? ReadPacket(SerialPort port)
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

    var packet = new SensorPacket();
    Array.Copy(buffer, 0, packet.Mac, 0, MAC_SIZE);
    packet.Temperature = BitConverter.ToSingle(buffer, MAC_SIZE + 0);
    packet.Humidity = BitConverter.ToSingle(buffer, MAC_SIZE + 4);
    packet.CO2 = BitConverter.ToUInt16(buffer, MAC_SIZE + 8);
    packet.BatteryLevel = BitConverter.ToSingle(buffer, MAC_SIZE + 12);

    return packet;
  }
}
