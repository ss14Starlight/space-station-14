using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared._ST.CosmicCult.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;

namespace Content.Server._ST.CosmicCult.EntitySystems;

/// <summary>
/// Makes the person with this component take damage over time.
/// Used for status effect.
/// </summary>
public sealed partial class CosmicEntropyDegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CosmicEntropyDebuffStatusEffectComponent, StatusEffectAppliedEvent>(OnInit);
    }

    private void OnInit(Entity<CosmicEntropyDebuffStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _damageable.TryChangeDamage(args.Target, ent.Comp.Degen, true, false);
        ent.Comp.CheckTimer = _timing.CurTime + ent.Comp.CheckWait;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CosmicEntropyDebuffStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out var _, out var component, out var statusEffect))
        {
            if (_timing.CurTime < component.CheckTimer)
                continue;
            if (statusEffect.AppliedTo is not { } target)
                continue;

            component.CheckTimer = _timing.CurTime + component.CheckWait;
            _damageable.TryChangeDamage(target, component.Degen, true, false);
        }
    }
}
