using Robust.Shared.GameStates;
using Robust.Shared.Utility;

#region Starlight
using Content.Shared.Starlight.Utility;
using Robust.Shared.Serialization;
#endregion Starlight

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Provides basic visuals for hitscan weapons - works with <see cref="HitscanBasicRaycastComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanBasicVisualsComponent : Component
{
    /// <summary>
    /// The muzzle flash from the hitscan weapon.
    /// </summary>
    [DataField]
    public SpriteSpecifier? MuzzleFlash;

    /// <summary>
    /// The "travel" sprite, this gets repeated until it hits the target.
    /// </summary>
    [DataField]
    public SpriteSpecifier? TravelFlash;

    /// <summary>
    /// The sprite that gets shown on the impact of the laser.
    /// </summary>
    [DataField]
    public SpriteSpecifier? ImpactFlash;

    // Starlight start
    /// <summary>
    /// The bullet that gets displayed for the hitscan rapidly for the client
    /// </summary>
    [DataField]
    public ExtendedSpriteSpecifier? Bullet;

    /// <summary>
    /// The speed of the projectile to display if Bullet has a value
    /// </summary>
    [DataField]
    public float Speed = 315f; // 9mm bullet speed
    // Starlight end
}
