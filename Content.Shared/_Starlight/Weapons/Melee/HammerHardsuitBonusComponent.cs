using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Melee;

/// <summary>
/// Starlight: Marker component added to hardsuits. When worn alongside a breaching hammer
/// (<see cref="BreachingHammerComponent"/>), the <see cref="HammerHardsuitBonusSystem"/>
/// reduces the hammer's movement speed penalty from 40% down to 35%.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HammerHardsuitBonusComponent : Component
{
}
