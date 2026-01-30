using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this will be able to ricochet off of things like walls which have RicochetableComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanRicochetComponent : Component
{
    /// <summary>
    /// The chance of a ricochet occurring.
    /// </summary>
    [DataField]
    public float Chance = 0f;
}
