using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this component will attempt to heat and ignite the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanIgniteEffectComponent : Component
{
    /// <summary>
    /// The effective temperature of the projectile
    /// </summary>
    [DataField]
    public float Temperature = 1050f;
}
