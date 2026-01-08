using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.DoAfterSpeedModifier;

/// <summary>
/// Modifies the speed at which an entity performs DoAfter actions.
/// A multiplier of 1.1 means actions complete 10% faster (delay / 1.1 = 0.91x original time).
/// Does NOT apply to self-healing actions.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DoAfterSpeedModifierComponent : Component
{
    /// <summary>
    /// Speed multiplier for DoAfter actions.
    /// Values > 1.0 make actions faster, values < 1.0 make them slower.
    /// </summary>
    [DataField]
    public float SpeedModifier = 1.0f;
}
