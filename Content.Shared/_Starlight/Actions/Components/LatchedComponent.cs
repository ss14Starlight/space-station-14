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

    /// <summary>
    /// Target's movement speed multiplier while latched. 0 pins them.
    /// Networked for client prediction.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float SpeedMultiplier;
}
