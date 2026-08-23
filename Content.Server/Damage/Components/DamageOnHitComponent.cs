using Content.Shared.Damage;

namespace Content.Server.Damage.Components;

/// <summary>
/// Damages the held item by a set amount when it hits someone. Can be used to make melee items limited-use.
/// </summary>
[RegisterComponent]
public sealed partial class DamageOnHitComponent : Component
{
    [DataField("ignoreResistances")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IgnoreResistances = true;

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}

