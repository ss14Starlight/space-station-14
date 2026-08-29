using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Applied to a latch target. Tracks the latcher and the blocked hand item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LatchedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Latcher;

    [ViewVariables]
    public EntityUid? BlockedHandItem;
}
