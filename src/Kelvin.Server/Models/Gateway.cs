namespace Kelvin.Server.Models;

public class Gateway : Entity
{
  // The mac address of the gateway
  public string? MacAddress { get; set; }

  /// <summary>
  /// The GPIO pin used by the heating relay
  /// </summary>
  public int? HeatingPin { get; set; }

  /// <summary>
  /// The GPIO pin used by the fan relay
  /// </summary>
  public int? FanPin { get; set; }

  /// <summary>
  /// The GPIO pin used by the cooling relay
  /// </summary>
  /// <remarks>
  /// Not all furnaces have cooling.
  /// </remarks>
  public int? CoolingPin { get; set; }

  /// <summary>
  /// The GPIO pin used for control
  /// </summary>
  public int? ControlPin { get; set; }
}
