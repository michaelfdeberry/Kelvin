using FakeItEasy;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Gateways;

public class GetRelayStatesTests
{
    [Fact]
    public async Task ReturnsRelayControllerState()
    {
        var relayController = A.Fake<IRelayController>();
        A.CallTo(() => relayController.GetState()).Returns(new RelayState
        {
            Heating = true,
            Cooling = false,
            Fan = null,
            Control = true,
        });

        var result = await new GetRelayStatesHandler(relayController).HandleAsync(new GetRelayStatesRequest());

        result.Value.ShouldBe(new GetRelayStatesResponse(true, false, null, true));
    }
}