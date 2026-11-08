using Content.Shared.Damage.Systems;

namespace Content.Server._Starlight.Electrocution;

public sealed partial class DamagedOnShockSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    [SubscribeLocalEvent]
    private void OnShockDealt(Entity<DamagedOnShockComponent> ent, ref ElectrifiedShockDealtEvent args)
        => _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage, ent.Comp.IgnoreResistances, interruptsDoAfters: false);
}
