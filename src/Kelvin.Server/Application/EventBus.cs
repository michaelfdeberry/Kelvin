namespace Kelvin.Server.Application;

/// <summary>
/// Publishes an event to every registered <see cref="IEventHandler{TEvent}" />, independently of the others.
/// </summary>
public interface IEventBus
{
  Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles one published event type. Multiple handlers may be registered for the same event type.
/// </summary>
public interface IEventHandler<in TEvent>
{
  Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

public class EventBus(IServiceScopeFactory scopeFactory, ILogger<EventBus> logger) : IEventBus
{
  public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
  {
    using var scope = scopeFactory.CreateScope();
    foreach (var handler in scope.ServiceProvider.GetServices<IEventHandler<TEvent>>())
    {
      try
      {
        await handler.HandleAsync(@event, cancellationToken);
      }
      catch (Exception ex)
      {
        // One destination failing (e.g. a disconnected SignalR client) must never stop the others from receiving it.
        logger.LogError(ex, "Event handler {Handler} failed handling {Event}", handler.GetType().Name, typeof(TEvent).Name);
      }
    }
  }
}
