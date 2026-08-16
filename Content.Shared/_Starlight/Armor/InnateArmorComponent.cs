using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Armor;

/// <summary>
/// Provides armor directly to an entity without requiring an ArmorComponent on worn equipment.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedArmorSystem))]
public sealed partial class InnateArmorComponent : Component
{
    /// <summary>
    /// The armor damage modifiers.
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;
}