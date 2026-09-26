using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Applied to a latch target. Tracks the latcher.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LatchedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Latcher;
}
