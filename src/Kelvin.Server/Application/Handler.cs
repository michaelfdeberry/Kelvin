namespace Kelvin.Server.Application;

/// <summary>
/// Represents a handler for a specific request type.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public interface IHandler<in TRequest>
  where TRequest : IRequest
{
  Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a handler for a specific request type that returns a response of type TResponse.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IHandler<in TRequest, TResponse>
  where TRequest : IRequest<TResponse>
{
  Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

public interface IPagedHandler<TRequest, TResponse>
  where TRequest : IRequest<PagedResult<TResponse>>
{
  Task<PagedResult<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
