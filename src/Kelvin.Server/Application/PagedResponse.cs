namespace Kelvin.Server.Application;

/// <summary>
/// The paging limits every list endpoint shares.
/// </summary>
public static class Paging
{
  public const int DefaultPage = 1;
  public const int DefaultPageSize = 50;
  public const int MaxPageSize = 200;
}

/// <summary>
/// The paging arguments of a request, normalized before they reach a query.
/// </summary>
/// <remarks>
/// Requests carry the raw values a caller supplied, so they must be run through <see cref="Normalize" /> before
/// being applied. An unbounded page size lets a single request pull the whole table into memory, so it is clamped
/// rather than trusted.
/// </remarks>
public record PagedRequestOptions(int Page = Paging.DefaultPage, int PageSize = Paging.DefaultPageSize)
{
  public int Skip => (Page - 1) * PageSize;

  public PagedRequestOptions Normalize() => new(Math.Max(Page, Paging.DefaultPage), Math.Clamp(PageSize, 1, Paging.MaxPageSize));
}
