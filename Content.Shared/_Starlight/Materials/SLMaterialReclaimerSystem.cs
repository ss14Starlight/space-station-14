using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Materials;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Starlight.Materials;

public sealed partial class SLMaterialReclaimerSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MaterialReclaimerComponent,RecyclerTryGibEvent>(OnTryGib);
    }

    private void OnTryGib(Entity<MaterialReclaimerComponent> ent, ref RecyclerTryGibEvent args)
    {
        args.Handled = true;
        if (_mobState.IsDead(args.Victim)) return;
        var damageSpecifier = new DamageSpecifier(ent.Comp.EmagDamage);
        _damageable.TryChangeDamage(args.Victim, damageSpecifier);
    }
}
