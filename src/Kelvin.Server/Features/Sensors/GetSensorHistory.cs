using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record SensorPacketDto(
  Guid Id,
  Guid? SensorId,
  string? SensorName,
  float TemperatureC,
  float HumidityPercentage,
  ushort CO2LevelPpm,
  DateTimeOffset CreatedAt
);

public record GetSensorHistoryRequest(
  DateTimeOffset? From = null,
  DateTimeOffset? To = null,
  Guid? SensorId = null,
  int Page = Paging.DefaultPage,
  int PageSize = Paging.DefaultPageSize
) : IPagedRequest<SensorPacketDto>;

public static class GetSensorHistoryErrors
{
  public static readonly Error InvalidRange = new("GetSensorHistory.InvalidRange", "The start of the range must not be after the end of it.");
}

/// <summary>
/// Returns the recorded sensor packets, most recent first.
/// </summary>
public class GetSensorHistoryHandler(KelvinContext context) : IPagedHandler<GetSensorHistoryRequest, SensorPacketDto>
{
  public async Task<PagedResult<SensorPacketDto>> HandleAsync(GetSensorHistoryRequest request, CancellationToken cancellationToken = default)
  {
    if (request.From is not null && request.To is not null && request.From > request.To)
      return PagedResult<SensorPacketDto>.Failure(GetSensorHistoryErrors.InvalidRange);

    // The caller's paging arguments are untrusted; an unbounded page size would read the whole table.
    var paging = new PagedRequestOptions(request.Page, request.PageSize).Normalize();

    var query = context.SensorPackets.AsNoTracking().Where(packet => packet.DeletedAt == null && packet.SensorId != null);

    if (request.SensorId is not null)
      query = query.Where(packet => packet.SensorId == request.SensorId);

    if (request.From is not null)
      query = query.Where(packet => packet.CreatedAt >= request.From);

    if (request.To is not null)
      query = query.Where(packet => packet.CreatedAt <= request.To);

    var totalCount = await query.CountAsync(cancellationToken);

    var packets = await query
      .OrderByDescending(packet => packet.CreatedAt)
      .Skip(paging.Skip)
      .Take(paging.PageSize)
      .Select(packet => new SensorPacketDto(
        packet.Id,
        packet.SensorId,
        packet.Sensor!.Name,
        packet.TemperatureC,
        packet.HumidityPercentage,
        packet.CO2LevelPpm,
        packet.CreatedAt
      ))
      .ToListAsync(cancellationToken);

    return PagedResult<SensorPacketDto>.Success(packets, paging.Page, paging.PageSize, totalCount);
  }
}

public class GetSensorHistoryEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/sensors/readings/history",
        async (
          IPagedHandler<GetSensorHistoryRequest, SensorPacketDto> handler,
          CancellationToken ct,
          [FromQuery] DateTimeOffset? from = null,
          [FromQuery] DateTimeOffset? to = null,
          [FromQuery] Guid? sensorId = null,
          [FromQuery] int page = Paging.DefaultPage,
          [FromQuery] int pageSize = Paging.DefaultPageSize
        ) =>
        {
          var result = await handler.HandleAsync(new GetSensorHistoryRequest(from, to, sensorId, page, pageSize), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetSensorHistoryErrors.InvalidRange)
              return Results.BadRequest(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result);
        }
      )
      .WithName("GetSensorHistory")
      .WithTags("Sensors");
  }
}

public class GetSensorHistoryRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IPagedHandler<GetSensorHistoryRequest, SensorPacketDto>, GetSensorHistoryHandler>();
  }
}
