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

  /// <summary>
  /// The minimum number of minutes to delay after a heating or cooling relay is turned off before it can be turned on again.
  /// </summary>
  public int? MinimumOffDurationMinutes { get; set; } = 5;

  /// <summary>
  /// The minimum number of minutes to keep a heating or cooling relay on before it can be turned off again.
  /// </summary>
  public int? MinimumOnDurationMinutes { get; set; } = 3;
}
