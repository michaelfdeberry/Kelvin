using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kelvin.Server.Tests.Services;

public class WeatherRefreshServiceTests
{
    private static void ForecastRequested(IDispatcher dispatcher) =>
        A.CallTo(() =>
                dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();

    // The service waits a long interval after its first pass, so the first request happens promptly on start.
    private static async Task WaitForFirstRequestAsync(IDispatcher dispatcher)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                ForecastRequested(dispatcher);
                return;
            }
            catch (Exception)
            {
                await Task.Delay(20);
            }
        }

        ForecastRequested(dispatcher);
    }

    [Fact]
    public async Task RequestsTheForecastOnStartup_SoTheCacheIsWarm()
    {
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() =>
                dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(
                Result<GetWeatherForecastResponse>.Failure(
                    GetCurrentLocationErrors.LocationNotConfigured
                )
            );
        var service = new WeatherRefreshService(
            NullLogger<WeatherRefreshService>.Instance,
            TimeProvider.System,
            dispatcher
        );

        await service.StartAsync(CancellationToken.None);
        await WaitForFirstRequestAsync(dispatcher);
        await service.StopAsync(CancellationToken.None);

        ForecastRequested(dispatcher);
    }
}
