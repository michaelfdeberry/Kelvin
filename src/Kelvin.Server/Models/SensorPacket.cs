namespace Kelvin.Server.Models;

public class SensorPacket
{
  public byte[] Mac { get; } = new byte[6];
  public float Temperature { get; set; }
  public float Humidity { get; set; }
  public ushort CO2 { get; set; }
  public float BatteryLevel { get; set; }

  public override string ToString()
  {
    var macString = BitConverter.ToString(Mac).Replace('-', ':').ToLowerInvariant();
    return $"MAC={macString}, Temp={Temperature:F2}, Humidity={Humidity:F2}, CO2={CO2}, Battery={BatteryLevel:F2}";
  }
}
