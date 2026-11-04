using System.Text.Json;

namespace Kelvin.Simulator;

internal sealed class KelvinServerClient(
    string serverUrl,
    HttpClient client,
    Action<DebugLevel, string> log
)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GatewayStatus> RefreshAsync(
        GatewayStatus previous,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return previous;

        try
        {
            var thermostat = await GetJsonAsync<ThermostatSnapshot>(
                "/api/thermostat",
                cancellationToken
            );
            var control = await GetJsonAsync<ControlStateSnapshot>(
                "/api/control/state",
                cancellationToken
            );
            var history = await GetJsonAsync<ControlHistoryPageSnapshot>(
                "/api/control/history?kind=Call&pageSize=1",
                cancellationToken
            );
            return previous with
            {
                Thermostat = thermostat,
                Control = control,
                CallContext = history?.Items?.FirstOrDefault(),
            };
        }
        catch (Exception exception)
        {
            log(
                DebugLevel.Info,
                $"RefreshServerStateAsync failed: {exception.GetType().Name}: {exception.Message}"
            );
            return previous;
        }
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(serverUrl), path);
        using var response = await client.GetAsync(uri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        log(
            DebugLevel.Verbose,
            $"GET {uri} -> {(int)response.StatusCode} {response.StatusCode}\n{body}"
        );
        return response.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<T>(body, JsonOptions)
            : default;
    }
}
