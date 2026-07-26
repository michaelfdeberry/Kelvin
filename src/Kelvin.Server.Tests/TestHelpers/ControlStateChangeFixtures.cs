using Kelvin.Server.Models;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Factory helpers for building <see cref="ControlStateChange"/> history used by the control feature tests.
/// </summary>
public static class ControlStateChangeFixtures
{
    /// <summary>
    /// Builds a recorded change on the call axis.
    /// </summary>
    /// <remarks>
    /// <see cref="Entity.CreatedAt"/> is owned by the interceptor, which always overwrites it, so a change that
    /// needs a specific position on the timeline cannot simply be saved through the context - the test has to
    /// advance the harness clock to the moment it wants and save then.
    /// </remarks>
    public static ControlStateChange CreateCall(
        ControlState state,
        ControlState? previousState = null
    ) =>
        new()
        {
            Kind = ControlChangeKind.Call,
            State = state,
            PreviousState = previousState,
        };

    public static ControlStateChange CreateControl(
        ControlState state,
        ControlState? previousState = null
    ) =>
        new()
        {
            Kind = ControlChangeKind.Control,
            State = state,
            PreviousState = previousState,
        };

    public static ControlStateChange CreateFan(
        ControlState state,
        ControlState? previousState = null
    ) =>
        new()
        {
            Kind = ControlChangeKind.Fan,
            State = state,
            PreviousState = previousState,
        };
}
