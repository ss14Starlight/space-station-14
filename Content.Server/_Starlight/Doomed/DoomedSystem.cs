using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Doomed;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;

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
        SubscribeLocalEvent<DoomedComponent, StatusEffectRemovedEvent>(OnStatusEffectRemoved);
    }

    private void OnMapInit(Entity<DoomedComponent> ent, ref MapInitEvent args) => _statusEffects.TryAddStatusEffectDuration(ent.Owner, ent.Comp.StatusEffect, ent.Comp.TimeToDeath);

    private void OnStatusEffectRemoved(EntityUid uid, DoomedComponent doomed, ref StatusEffectRemovedEvent args)
    {
        if (HasComp<TransformComponent>(args.Target))
            Spawn(doomed.DamageEffect, Transform(args.Target).Coordinates);

        _damageable.TryChangeDamage(uid, doomed.Damage);
    }
}
