using Kelvin.Server.Features.Gateways;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Factory helpers for building <see cref="GetGatewayResponse"/> fixtures used by the control tests.
/// </summary>
public static class ControlFixtures
{
    public const int MinimumOnMinutes = 3;
    public const int MinimumOffMinutes = 5;

    public static GetGatewayResponse CreateGateway(
        int? minimumOnDurationMinutes = MinimumOnMinutes,
        int? minimumOffDurationMinutes = MinimumOffMinutes,
        int? heatingPin = 17,
        int? coolingPin = 27,
        int? fanPin = 22,
        int? controlPin = 23
    ) =>
        new(
            "aa:bb:cc:dd:ee:ff",
            heatingPin,
            fanPin,
            coolingPin,
            controlPin,
            minimumOffDurationMinutes,
            minimumOnDurationMinutes
        );
}
