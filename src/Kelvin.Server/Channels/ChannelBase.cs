using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Kelvin.Server.Channels;

public interface IChannelBase<T>
{
  Task WriteAsync(T item, CancellationToken cancellation = default);
  Task<T> ReadAsync(Guid subscriberId, CancellationToken cancellationToken = default);
}

public abstract class ChannelBase<T>(ILogger<ChannelBase<T>> logger) : IDisposable, IChannelBase<T>
{
  private readonly ConcurrentDictionary<Guid, Channel<T>> _subscribers = new();

  public async Task WriteAsync(T item, CancellationToken cancellation = default)
  {
    // Broadcast to all subscribers
    var tasks = new List<Task>();

    foreach (var subscriber in _subscribers.Values)
    {
      tasks.Add(
        Task.Run(
          async () =>
          {
            try
            {
              await subscriber.Writer.WriteAsync(item, cancellation);
            }
            catch (Exception ex)
            {
              logger.LogError(ex, "Error writing to subscriber channel.");
            }
          },
          cancellation
        )
      );
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks);
    }
  }

  public async Task<T> ReadAsync(Guid subscriberId, CancellationToken cancellationToken = default)
  {
    if (!_subscribers.TryGetValue(subscriberId, out var channel))
    {
      channel = Channel.CreateUnbounded<T>(
        new UnboundedChannelOptions
        {
          SingleReader = true,
          SingleWriter = false,
          AllowSynchronousContinuations = true,
        }
      );
      _subscribers.TryAdd(subscriberId, channel);
    }

    return await channel.Reader.ReadAsync(cancellationToken);
  }

  private bool _disposed;

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      if (disposing)
      {
        foreach (var subscriber in _subscribers.Values)
        {
          subscriber.Writer.TryComplete();
        }
        _subscribers.Clear();
      }
      _disposed = true;
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
}
