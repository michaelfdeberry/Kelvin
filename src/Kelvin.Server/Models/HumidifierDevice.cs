using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

public class HumidifierDevice : Device
{
  [NotMapped]
  public HumidifierConfiguration Configuration
  {
    get => DeviceConfiguration.Deserialize<HumidifierConfiguration>(ConfigurationJson);
    set => ConfigurationJson = value.Serialize();
  }
}
