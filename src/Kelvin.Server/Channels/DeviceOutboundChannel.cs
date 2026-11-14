using Kelvin.Protocol;
using Kelvin.Server.Application;
using Kelvin.Server.Models;

namespace Kelvin.Server.Channels;

/// <summary>One physical frame ready for the radio: frame tag, envelope header, and chunk payload.</summary>
public record DeviceOutboundFrame(string MacAddress, byte[] Payload);

/// <summary>
/// A bounded latest-state queue shared by every device type. GatewayService is the only reader; once a message
/// starts draining, all its chunks are retained in order while newer pending snapshots replace older ones.
/// </summary>
public interface IDeviceOutboundChannel
{
  /// <param name="frameTag">The device type's <see cref="FrameTags" /> entry; message types are scoped to it.</param>
  /// <param name="coalesce">Replace a pending message of the same device and type instead of queuing behind it.</param>
  void Write(string macAddress, byte[] frameTag, byte messageType, byte[] payload, bool coalesce = true);
  bool TryRead(out DeviceOutboundFrame frame);
}

public class DeviceOutboundChannel : IDeviceOutboundChannel
{
  public const int MaxPendingMessages = 128;
  public const int MaxPendingPayloadBytes = 256 * 1024;

  private sealed record PendingMessage(string MacAddress, byte[] FrameTag, byte MessageType, byte[] Payload, bool Coalesce);

  private readonly object _lock = new();
  private readonly LinkedList<PendingMessage> _pending = new();
  private readonly Dictionary<(string MacAddress, uint FrameTag, byte MessageType), LinkedListNode<PendingMessage>> _byKey = new();
  private Queue<byte[]> _activeFrames = new();
  private string _activeMacAddress = string.Empty;
  private byte[] _activeFrameTag = [];
  private int _pendingPayloadBytes;

  public void Write(string macAddress, byte[] frameTag, byte messageType, byte[] payload, bool coalesce = true)
  {
    ArgumentOutOfRangeException.ThrowIfNotEqual(frameTag.Length, FrameTags.Size);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, DeviceEnvelope.MaxPayloadSize);

    lock (_lock)
    {
      var message = new PendingMessage(macAddress, frameTag.ToArray(), messageType, payload.ToArray(), coalesce);
      var key = KeyOf(message);
      if (coalesce && _byKey.TryGetValue(key, out var existing))
        RemovePending(existing);

      while (_pending.Count >= MaxPendingMessages || _pendingPayloadBytes + payload.Length > MaxPendingPayloadBytes)
        RemovePending(_pending.First!);

      var node = _pending.AddLast(message);
      if (coalesce)
        _byKey.Add(key, node);
      _pendingPayloadBytes += payload.Length;
    }
  }

  public bool TryRead(out DeviceOutboundFrame frame)
  {
    lock (_lock)
    {
      if (_activeFrames.Count == 0)
      {
        if (_pending.First is not { } next)
        {
          frame = null!;
          return false;
        }

        var pending = next.Value;
        RemovePending(next);
        _activeMacAddress = pending.MacAddress;
        _activeFrameTag = pending.FrameTag;
        _activeFrames = new Queue<byte[]>(DeviceEnvelope.Encode(pending.MessageType, pending.Payload));
      }

      var chunk = _activeFrames.Dequeue();
      frame = new DeviceOutboundFrame(_activeMacAddress, [.. _activeFrameTag, .. chunk]);
      return true;
    }
  }

  private static (string, uint, byte) KeyOf(PendingMessage message) =>
    (message.MacAddress.ToUpperInvariant(), BitConverter.ToUInt32(message.FrameTag), message.MessageType);

  private void RemovePending(LinkedListNode<PendingMessage> node)
  {
    _pending.Remove(node);
    if (node.Value.Coalesce)
      _byKey.Remove(KeyOf(node.Value));
    _pendingPayloadBytes -= node.Value.Payload.Length;
  }
}

public class DeviceOutboundChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IDeviceOutboundChannel, DeviceOutboundChannel>();
  }
}
