using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Wrapper around <see cref="NavInterfaceState"/>
/// </summary>
[Serializable, NetSerializable]
public sealed class NavBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState State;
    public DockingPortStates DockStates;

    public NavBoundUserInterfaceState(NavInterfaceState state, DockingPortStates dockStates)
    {
        State = state;
        DockStates = dockStates;
    }
}
