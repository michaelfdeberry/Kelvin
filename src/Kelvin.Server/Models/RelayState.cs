namespace Kelvin.Server.Models;

public class RelayState
{
  public bool? Control { get; set; }
  public bool? Heating { get; set; }
  public bool? Cooling { get; set; }
  public bool? Fan { get; set; }
}
