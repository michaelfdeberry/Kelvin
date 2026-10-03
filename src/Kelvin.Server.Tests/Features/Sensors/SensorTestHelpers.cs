using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Messaging;

namespace Kelvin.Server.Tests.Features.Sensors;

internal static class SensorTestHelpers
{
    public static IEventBus CreateEventBus() => A.Fake<IEventBus>();
}