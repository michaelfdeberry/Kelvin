namespace Kelvin.Server.Application;

/// <summary>
/// Represents a dispatcher that routes requests to their corresponding handlers.
/// </summary>
public interface IDispatcher
{
  Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest<TResponse>;

  Task<Result> DispatchAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest;

  Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}

public class Dispatcher(IServiceScopeFactory scopeFactory, ILogger<Dispatcher> logger) : IDispatcher
{
  /// <summary>
  /// Dispatches a request to its corresponding handler and returns the result.
  /// </summary>
  public async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest<TResponse>
  {
    using var scope = scopeFactory.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IHandler<TRequest, TResponse>>();
    return await handler.HandleAsync(request, cancellationToken);
  }

  /// <summary>
  /// Dispatches a request to its corresponding handler and returns the result.
  /// </summary>
  public async Task<Result> DispatchAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest
  {
    using var scope = scopeFactory.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IHandler<TRequest>>();
    return await handler.HandleAsync(request, cancellationToken);
  }

  /// <summary>
  /// Publishes an event to all registered handlers.
  /// </summary>
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
