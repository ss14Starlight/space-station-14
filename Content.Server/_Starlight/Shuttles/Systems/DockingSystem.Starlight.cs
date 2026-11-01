using Content.Server._Starlight.Shuttles.Systems;

// ReSharper disable CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    [Dependency] private RadarDockingStateSystem _radarDockingState = null!;

    private void InvalidateRadarDockingPortStates() =>
        _radarDockingState.InvalidateDockingPortStates();
}
