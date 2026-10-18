using Kelvin.Server.Application;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Models;

namespace Kelvin.Server.Channels;

public record HmiOutboundMessage(string MacAddress, byte[] Payload);

/// <summary>
/// A bounded latest-state queue. GatewayService is the only reader; once a message starts draining,
/// all its chunks are retained in order while newer pending snapshots replace older ones.
/// </summary>
public interface IHmiOutboundChannel
{
  void Write(string macAddress, HmiMessageType messageType, byte[] payload);
  bool TryRead(out HmiOutboundMessage message);
}

public class HmiOutboundChannel : IHmiOutboundChannel
{
  public const int MaxPendingMessages = 128;
  public const int MaxPendingPayloadBytes = 256 * 1024;

  private sealed record PendingMessage(string MacAddress, HmiMessageType MessageType, byte[] Payload);

  private readonly object _lock = new();
  private readonly LinkedList<PendingMessage> _pending = new();
  private readonly Dictionary<(string MacAddress, HmiMessageType MessageType), LinkedListNode<PendingMessage>> _byKey = new();
  private Queue<byte[]> _activeFrames = new();
  private string _activeMacAddress = string.Empty;
  private int _pendingPayloadBytes;

  public void Write(string macAddress, HmiMessageType messageType, byte[] payload)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, HmiEnvelope.MaxPayloadSize);

    lock (_lock)
    {
      var key = (macAddress.ToUpperInvariant(), messageType);
      if (_byKey.TryGetValue(key, out var existing))
        RemovePending(existing);

      while (_pending.Count >= MaxPendingMessages || _pendingPayloadBytes + payload.Length > MaxPendingPayloadBytes)
        RemovePending(_pending.First!);

      var node = _pending.AddLast(new PendingMessage(macAddress, messageType, payload.ToArray()));
      _byKey.Add(key, node);
      _pendingPayloadBytes += payload.Length;
    }
  }

  public bool TryRead(out HmiOutboundMessage message)
  {
    lock (_lock)
    {
      if (_activeFrames.Count == 0)
      {
        if (_pending.First is not { } next)
        {
          message = null!;
          return false;
        }

        var pending = next.Value;
        RemovePending(next);
        _activeMacAddress = pending.MacAddress;
        _activeFrames = new Queue<byte[]>(HmiEnvelope.Encode(pending.MessageType, pending.Payload));
      }

      var payload = _activeFrames.Dequeue();
      var frame = new byte[FrameTags.Size + payload.Length];
      Buffer.BlockCopy(FrameTags.Hmi, 0, frame, 0, FrameTags.Size);
      Buffer.BlockCopy(payload, 0, frame, FrameTags.Size, payload.Length);
      message = new HmiOutboundMessage(_activeMacAddress, frame);
      return true;
    }
  }

  private void RemovePending(LinkedListNode<PendingMessage> node)
  {
    _pending.Remove(node);
    _byKey.Remove((node.Value.MacAddress.ToUpperInvariant(), node.Value.MessageType));
    _pendingPayloadBytes -= node.Value.Payload.Length;
  }
}

public class HmiOutboundChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IHmiOutboundChannel, HmiOutboundChannel>();
  }
}
