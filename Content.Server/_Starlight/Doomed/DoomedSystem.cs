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
        SubscribeLocalEvent<DoomedComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<DoomedComponent, StatusEffectRemovedEvent>(OnStatusEffectRemoved);
    }

    private void OnMapInit(Entity<DoomedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TimeApplied = _timing.CurTime;
        _statusEffects.TryAddStatusEffectDuration(ent.Owner, ent.Comp.StatusEffect, ent.Comp.TimeToDeath);
    }

    private void OnComponentShutdown(Entity<DoomedComponent> ent, ref ComponentShutdown args)
    {
        _statusEffects.TryRemoveStatusEffect(ent.Owner, ent.Comp.StatusEffect);
    }
    
    private void OnStatusEffectRemoved(Entity<DoomedComponent> ent, ref StatusEffectRemovedEvent args)
    {
        // if this has happened, the component was removed externally before the status effect ran out
        if (_timing.CurTime - ent.Comp.TimeApplied < ent.Comp.TimeToDeath) return;

        Spawn(ent.Comp.DamageEffect, Transform(ent.Owner).Coordinates);
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage);
    }
}