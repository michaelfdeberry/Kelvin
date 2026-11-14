using System.Net;
using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class KelvinServerClientTests
{
    [Fact]
    public async Task Refresh_DeserializesWebJsonAndUsesCallHistoryContext()
    {
        var paths = new List<string>();
        using var http = new HttpClient(
            new StubHandler(request =>
            {
                var path = request.RequestUri!.PathAndQuery;
                paths.Add(path);
                return Ok(
                    path switch
                    {
                        "/api/thermostat" =>
                            """{"mode":"Heating","fanEnabled":true,"hysteresisC":0.6}""",
                        "/api/control/state" =>
                            """{"controlState":"Enable","callState":"Heating","fanOn":true,"lastChange":{"targetTemperatureC":99,"hysteresisC":9}}""",
                        _ => """{"items":[{"targetTemperatureC":24,"hysteresisC":0.8}]}""",
                    }
                );
            })
        );
        var client = new KelvinServerClient("http://localhost:5000", http, (_, _) => { });

        var status = await client.RefreshAsync(new GatewayStatus(), CancellationToken.None);

        status.Thermostat.ShouldBe(new ThermostatSnapshot("Heating", true, 0.6f));
        status.Control.ShouldNotBeNull();
        status.Control.CallState.ShouldBe("Heating");
        status.CallContext.ShouldBe(new ControlStateChangeSnapshot(24f, 0.8f));
        paths.ShouldBe(
            new[]
            {
                "/api/thermostat",
                "/api/control/state",
                "/api/control/history?kind=Call&pageSize=1",
            }
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Refresh_FailurePreservesPreviousSnapshot(bool malformedJson)
    {
        var previous = new GatewayStatus(new ThermostatSnapshot("Cooling", false, 0.6f));
        var messages = new List<DebugLevel>();
        using var http = new HttpClient(
            new StubHandler(_ =>
                malformedJson ? Ok("invalid json") : throw new HttpRequestException("offline")
            )
        );
        var client = new KelvinServerClient(
            "http://localhost:5000",
            http,
            (level, _) => messages.Add(level)
        );

        var status = await client.RefreshAsync(previous, CancellationToken.None);

        status.ShouldBeSameAs(previous);
        messages.ShouldContain(DebugLevel.Info);
    }

    [Fact]
    public async Task Refresh_UnsuccessfulResponsesClearUnavailableFields()
    {
        using var http = new HttpClient(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        );
        var client = new KelvinServerClient("http://localhost:5000", http, (_, _) => { });

        var status = await client.RefreshAsync(
            new GatewayStatus(new ThermostatSnapshot("Heating", false, 0.6f)),
            CancellationToken.None
        );

        status.ShouldBe(new GatewayStatus());
    }

    [Fact]
    public async Task Refresh_EmptyUrlSkipsHttp()
    {
        using var http = new HttpClient(
            new StubHandler(_ => throw new InvalidOperationException("Unexpected request"))
        );
        var previous = new GatewayStatus();

        var status = await new KelvinServerClient("", http, (_, _) => { }).RefreshAsync(
            previous,
            CancellationToken.None
        );

        status.ShouldBeSameAs(previous);
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
