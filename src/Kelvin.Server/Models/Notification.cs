namespace Kelvin.Server.Models;

public enum NotificationType
{
  Information,
  Warning,
  Success,
  Error,
}

public class Notification : Entity
{
  public required string Message { get; set; }
  public NotificationType Type { get; set; }
  public bool Dismissible { get; set; }
  public string? Heading { get; set; }
  public bool? Banner { get; set; }
  public int? Duration { get; set; }
}
