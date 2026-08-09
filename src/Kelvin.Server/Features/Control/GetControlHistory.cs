using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Control;

public record GetControlHistoryRequest(
  DateTimeOffset? From = null,
  DateTimeOffset? To = null,
  ControlChangeKind? Kind = null,
  int Page = Paging.DefaultPage,
  int PageSize = Paging.DefaultPageSize
) : IPagedRequest<ControlStateChangeDto>;

public static class GetControlHistoryErrors
{
  public static readonly Error InvalidRange = new("GetControlHistory.InvalidRange", "The start of the range must not be after the end of it.");
}

/// <summary>
/// Returns the recorded control state changes, most recent first.
/// </summary>
public class GetControlHistoryHandler(KelvinContext context) : IPagedHandler<GetControlHistoryRequest, ControlStateChangeDto>
{
  public async Task<PagedResult<ControlStateChangeDto>> HandleAsync(GetControlHistoryRequest request, CancellationToken cancellationToken = default)
  {
    if (request.From is not null && request.To is not null && request.From > request.To)
      return PagedResult<ControlStateChangeDto>.Failure(GetControlHistoryErrors.InvalidRange);

    // The caller's paging arguments are untrusted; an unbounded page size would read the whole table.
    var paging = new PagedRequestOptions(request.Page, request.PageSize).Normalize();

    var query = context.ControlStateChanges.AsNoTracking().Where(change => change.DeletedAt == null);

    if (request.Kind is not null)
      query = query.Where(change => change.Kind == request.Kind);

    if (request.From is not null)
      query = query.Where(change => change.CreatedAt >= request.From);

    if (request.To is not null)
      query = query.Where(change => change.CreatedAt <= request.To);

    var totalCount = await query.CountAsync(cancellationToken);

    var changes = await query.OrderByDescending(change => change.CreatedAt).Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);

    var items = changes.Select(ControlStateChangeDto.FromEntity).ToList();

    return PagedResult<ControlStateChangeDto>.Success(items, paging.Page, paging.PageSize, totalCount);
  }
}

public class GetControlHistoryEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/control/history",
        async (
          IPagedHandler<GetControlHistoryRequest, ControlStateChangeDto> handler,
          CancellationToken ct,
          [FromQuery] DateTimeOffset? from = null,
          [FromQuery] DateTimeOffset? to = null,
          [FromQuery] ControlChangeKind? kind = null,
          [FromQuery] int page = Paging.DefaultPage,
          [FromQuery] int pageSize = Paging.DefaultPageSize
        ) =>
        {
          var result = await handler.HandleAsync(new GetControlHistoryRequest(from, to, kind, page, pageSize), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetControlHistoryErrors.InvalidRange)
              return Results.BadRequest(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result);
        }
      )
      .WithName("GetControlHistory")
      .WithTags("Control");
  }
}

public class GetControlHistoryRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IPagedHandler<GetControlHistoryRequest, ControlStateChangeDto>, GetControlHistoryHandler>();
  }
}
