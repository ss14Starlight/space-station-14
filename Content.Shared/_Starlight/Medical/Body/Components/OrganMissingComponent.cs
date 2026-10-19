using Content.Shared.Damage;
// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Medical.Body.Components;

[RegisterComponent]
public sealed partial class OrganMissingComponent : Component
{
    [DataField("damagePerSecond", required: true)]
    public DamageSpecifier DamagePerSecond = default!;
}
