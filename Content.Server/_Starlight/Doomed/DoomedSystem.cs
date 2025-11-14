using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Doomed;
using Robust.Shared.Timing;
using Content.Shared.Damage;

namespace Content.Server._Starlight.Doomed;

public sealed partial class DoomedSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoomedComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DoomedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TimeApplied = _timing.CurTime;
        _statusEffects.TryAddStatusEffectDuration(ent.Owner, ent.Comp.StatusEffect, ent.Comp.TimeToDeath);
        Timer.Spawn(ent.Comp.TimeToDeath, () => Die(ent));
    }

    private void Die(Entity<DoomedComponent> ent)
    {
        Spawn(ent.Comp.DamageEffect, Transform(ent.Owner).Coordinates);
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage);
    }
}