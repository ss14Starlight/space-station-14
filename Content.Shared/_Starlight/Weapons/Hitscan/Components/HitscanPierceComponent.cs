using Content.Shared._Starlight.Combat.Ranged.Pierce;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this will be able to pierce through things like people which have PiercableComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanPierceComponent : Component
{
    /// <summary>
    /// The chance of a ricochet occurring.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;

    /// <summary>
    /// The maximum deviation in radians when this projectile pierces
    /// </summary>
    [DataField]
    public float Deviation = 0.1f;

    /// <summary>
    /// The level of material this projectile can pierce through
    /// </summary>
    [DataField]
    public PierceLevel PierceLevel = PierceLevel.Flesh;
}
