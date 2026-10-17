namespace Kelvin.Server.Application;

/// <summary>
/// Represents a handler for a specific request type.
/// </summary>
public interface IHandler<in TRequest>
  where TRequest : IRequest
{
  Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a handler for a specific request type that returns a response of type TResponse.
/// </summary>
public interface IHandler<in TRequest, TResponse>
  where TRequest : IRequest<TResponse>
{
  Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a handler for a specific request type that returns a paged result.
/// </summary>
public interface IPagedHandler<TRequest, TResponse>
  where TRequest : IRequest<PagedResult<TResponse>>
{
  Task<PagedResult<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a handler for a specific event type.
/// </summary>
public interface IEventHandler<in TEvent>
{
  Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
