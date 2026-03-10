using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Melee;

/// <summary>
/// Starlight: Marker component on the breaching hammer.
/// Used by <see cref="HammerHardsuitBonusSystem"/> to detect when the hammer is picked up or
/// worn so it can adjust the speed penalty based on whether the user has a hardsuit on.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BreachingHammerComponent : Component
{
}
