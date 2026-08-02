using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Preferences;

public record UpdatePreferencesRequest(TimeFormat? TimeFormat, TemperatureUnit? TemperatureUnit, long? LocationId, string? LocationName)
  : IRequest<UpdatePreferencesResponse>;

public record UpdatePreferencesResponse(TimeFormat TimeFormat, TemperatureUnit TemperatureUnit, long? LocationId, string? LocationName);

public static class UpdatePreferencesErrors
{
  public static readonly Error DefaultError = new("UpdatePreferences.Failed", "An error occurred processing the request.");
}

public class UpdatePreferencesHandler(KelvinContext context) : IHandler<UpdatePreferencesRequest, UpdatePreferencesResponse>
{
  public async Task<Result<UpdatePreferencesResponse>> HandleAsync(UpdatePreferencesRequest request, CancellationToken ct = default)
  {
    var preferences = await context.Preferences.FirstOrDefaultAsync(ct);
    if (preferences is null)
    {
      preferences = new Models.Preferences();
      context.Preferences.Add(preferences);
    }

    preferences.TemperatureUnit = request.TemperatureUnit ?? preferences.TemperatureUnit;
    preferences.TimeFormat = request.TimeFormat ?? preferences.TimeFormat;
    preferences.LocationId = request.LocationId ?? preferences.LocationId;
    preferences.LocationName = request.LocationName ?? preferences.LocationName;

    var response = new UpdatePreferencesResponse(
      preferences.TimeFormat,
      preferences.TemperatureUnit,
      preferences.LocationId,
      preferences.LocationName
    );

    await context.SaveChangesAsync(ct);
    return Result<UpdatePreferencesResponse>.Success(response);
  }
}

public class UpdatePreferencesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/preferences",
        async (UpdatePreferencesRequest request, IHandler<UpdatePreferencesRequest, UpdatePreferencesResponse> handler, CancellationToken ct) =>
        {
          try
          {
            var result = await handler.HandleAsync(request, ct);

            if (result.IsFailure)
            {
              return Results.InternalServerError(result.Error);
            }

            return Results.Ok(result.Value);
          }
          catch (Exception)
          {
            return Results.InternalServerError(UpdatePreferencesErrors.DefaultError);
          }
        }
      )
      .WithName("UpdatePreferences")
      .WithTags("Preferences");
  }
}

public class UpdatePreferencesFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdatePreferencesRequest, UpdatePreferencesResponse>, UpdatePreferencesHandler>();
  }
}
