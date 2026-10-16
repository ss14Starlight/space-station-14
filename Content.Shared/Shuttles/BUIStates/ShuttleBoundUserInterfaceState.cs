using Content.Shared._Starlight.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public DockingPortStates DockingPortStates; // Starlight
    public bool SurfaceMode; // Starlight
    public NetEntity? SurfaceMapGrid; // Starlight

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, DockingPortStates dockingPortStates, bool surfaceMode, NetEntity? surfaceMapGrid) // Starlight: +dockingPortStates +surfaceMode +surfaceMapGrid
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        // Starlight - start
        DockingPortStates = dockingPortStates;
        SurfaceMode = surfaceMode;
        SurfaceMapGrid = surfaceMapGrid;
        // Starlight - end
    }
}
