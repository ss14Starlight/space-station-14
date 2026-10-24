using Content.Shared.Damage;

namespace Content.Server._Starlight.Electrocution;

/// <summary>
/// An electrified entity with this component takes <see cref="Damage"/> every time it shocks someone,
/// so electrified structures (grilles) wear out instead of holding a target forever.
/// </summary>
[RegisterComponent]
public sealed partial class DamagedOnShockComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public bool IgnoreResistances = true;
}
