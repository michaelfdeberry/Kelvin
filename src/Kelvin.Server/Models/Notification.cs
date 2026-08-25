namespace Kelvin.Server.Models;

public enum NotificationType
{
  Information,
  Warning,
  Success,
  Error,
}

/// <summary>
/// For now notifications will either display a toast on the UI or a pinned alert at the top of the page.
/// Both use the alert component, so the notification contract just matches the alert component properties.
/// </summary>
public record Notification(
  string Message,
  NotificationType Type = NotificationType.Information,
  bool Dismissible = true,
  string? Heading = null,
  bool? Banner = false,
  int? Duration = null
);
