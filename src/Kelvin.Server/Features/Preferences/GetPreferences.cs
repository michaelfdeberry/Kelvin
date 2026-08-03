using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Preferences;

public record GetPreferencesRequest() : IRequest<GetPreferencesResponse>;

public record GetPreferencesResponse(TemperatureUnit TemperatureUnit, TimeFormat TimeFormat, long? LocationId, string? LocationName);

public class GetPreferencesHandler(KelvinContext context) : IHandler<GetPreferencesRequest, GetPreferencesResponse>
{
  public async Task<Result<GetPreferencesResponse>> HandleAsync(GetPreferencesRequest request, CancellationToken ct = default)
  {
    var preferences = await context.Preferences.FirstOrDefaultAsync(ct);
    if (preferences is null)
    {
      preferences = new Models.Preferences();
      context.Preferences.Add(preferences);
      await context.SaveChangesAsync(ct);
    }

    var response = new GetPreferencesResponse(preferences.TemperatureUnit, preferences.TimeFormat, preferences.LocationId, preferences.LocationName);
    return Result<GetPreferencesResponse>.Success(response);
  }
}

public class GetPreferencesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/preferences",
        async (IHandler<GetPreferencesRequest, GetPreferencesResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetPreferencesRequest(), ct);

          return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }
      )
      .WithName("GetPreferences")
      .WithTags("Preferences");
  }
}

public class GetPreferencesRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetPreferencesRequest, GetPreferencesResponse>, GetPreferencesHandler>();
  }
}
