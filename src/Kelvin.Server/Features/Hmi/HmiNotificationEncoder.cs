using System.Text;
using Kelvin.Protocol;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

public static class HmiNotificationEncoder
{
  public static byte[] Encode(Notification notification, bool persistent = true) =>
    HmiCbor.Encode(
      8,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteByteString(notification.Id.ToByteArray());
        writer.WriteUInt32(2);
        writer.WriteTextString(BoundedText(notification.Message, 1024));
        writer.WriteUInt32(3);
        writer.WriteUInt32((uint)notification.Type);
        writer.WriteUInt32(4);
        writer.WriteBoolean(notification.Dismissible);
        writer.WriteUInt32(5);
        if (notification.Heading is null)
          writer.WriteNull();
        else
          writer.WriteTextString(BoundedText(notification.Heading, 128));
        writer.WriteUInt32(6);
        writer.WriteBoolean(notification.Banner == true);
        writer.WriteUInt32(7);
        if (notification.Duration is > 0)
          writer.WriteUInt32((uint)notification.Duration.Value);
        else
          writer.WriteNull();
        writer.WriteUInt32(8);
        writer.WriteBoolean(persistent);
      }
    );

  public static byte[] EncodeDismiss(Guid id) =>
    HmiCbor.Encode(
      1,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteByteString(id.ToByteArray());
      }
    );

  private static string BoundedText(string text, int capacity)
  {
    var buffer = new byte[capacity];
    Encoding.UTF8.GetEncoder().Convert(text.AsSpan(), buffer, true, out _, out var bytesUsed, out _);
    return Encoding.UTF8.GetString(buffer, 0, bytesUsed);
  }
}
