namespace Kelvin.Server.Application;

/// <summary>
/// Represents a dispatcher that routes requests to their corresponding handlers.
/// </summary>
/// <remarks>
/// For use when a scoped service is required to handle a request, such as a background service or signalR hub.
/// </remarks>
public interface IDispatcher
{
  Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest<TResponse>;

  Task<Result> DispatchAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest;
}

public class Dispatcher(IServiceScopeFactory scopeFactory) : IDispatcher
{
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

  public async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest<TResponse>
  {
    using var scope = _scopeFactory.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IHandler<TRequest, TResponse>>();
    return await handler.HandleAsync(request, cancellationToken);
  }

  public async Task<Result> DispatchAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest
  {
    using var scope = _scopeFactory.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IHandler<TRequest>>();
    return await handler.HandleAsync(request, cancellationToken);
  }
}
