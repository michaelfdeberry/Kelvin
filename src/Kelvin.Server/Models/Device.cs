using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

/// <summary>
/// A generic integrated device (e.g. a humidifier controller) reachable through the Gateway. Type-specific
/// settings live in <see cref="Configuration" /> as JSON whose shape is selected by <see cref="Type" />.
/// </summary>
public abstract class Device : Entity
{
  public string Name { get; set; } = string.Empty;

  public string? MacAddress { get; set; }

  public bool Enabled { get; set; } = true;

  /// <summary>When the device last reported in.</summary>
  public DateTimeOffset? LastSeenAt { get; set; }

  /// <summary>JSON backing store for each derived device's typed configuration property.</summary>
  [Column("Configuration")]
  public string ConfigurationJson { get; set; } = "{}";
}
