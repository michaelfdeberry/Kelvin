using System.Text.Json;

namespace Kelvin.Server.Models;

/// <summary>Base for the per-device JSON configuration stored in the Devices table.</summary>
public abstract class DeviceConfiguration
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  public static T Deserialize<T>(string? json)
    where T : DeviceConfiguration, new() =>
    string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();

  public string Serialize() => JsonSerializer.Serialize(this, GetType(), SerializerOptions);
}
