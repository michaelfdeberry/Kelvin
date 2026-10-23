using FakeItEasy;
using Kelvin.Server.Application;

namespace Kelvin.Server.Tests.Features.Sensors;

internal static class SensorTestHelpers
{
    public static IDispatcher CreateDispatcher() => A.Fake<IDispatcher>();
}
