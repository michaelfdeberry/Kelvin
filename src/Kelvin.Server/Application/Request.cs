namespace Kelvin.Server.Application;

public interface IRequest { }

public interface IRequest<TResponse> { }

public interface IPagedRequest<TResponse> : IRequest<PagedResult<TResponse>>
{
  int Page { get; }

  int PageSize { get; }
}
