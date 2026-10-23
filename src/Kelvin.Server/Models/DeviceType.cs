namespace Kelvin.Server.Models;

/// <summary>
/// The known hardware device types. Hardware has standardized to a small, fixed set, so capabilities (which
/// sensors/battery a device has) are derived from this at Sensor-creation time instead of being manually
/// configured per sensor.
/// </summary>
public enum DeviceType
{
  Node,
  Hmi,
  Kiosk,
}
