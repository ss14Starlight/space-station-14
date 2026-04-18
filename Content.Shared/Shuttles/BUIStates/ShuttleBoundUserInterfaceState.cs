using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public DockingPortStates DockingPortStates; // Starlight

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, DockingPortStates dockingPortStates) // Starlight: +dockingPortStates
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        DockingPortStates = dockingPortStates; // Starlight
    }
}
