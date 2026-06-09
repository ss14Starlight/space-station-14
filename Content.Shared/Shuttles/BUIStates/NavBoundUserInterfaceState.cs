using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Wrapper around <see cref="NavInterfaceState"/>
/// </summary>
[Serializable, NetSerializable]
public sealed class NavBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState State;
    public DockingPortStates DockingPortStates; // Starlight

    public NavBoundUserInterfaceState(NavInterfaceState state, DockingPortStates dockingPortStates) // Starlight: +dockingPortStates
    {
        State = state;
        DockingPortStates = dockingPortStates; // Starlight
    }
}
