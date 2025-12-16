using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanBasicDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanBasicDamageComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var dmg = ent.Comp.Damage * _damage.UniversalHitscanDamageModifier;

        // var damageDealt = _damage.TryChangeDamage(args.Data.HitEntity.Value, dmg, origin: args.Data.Gun); // Starlight - we redefine this
        // Starlight start
        var damageDealt = _damage.ChangeDamage(
                args.Data.HitEntity.Value,
                dmg,
                ignoreResistances: ent.Comp.IgnoreResistances,
                origin: args.Data.Gun,
                armorPenetration: ent.Comp.ArmorPenetration,
                canHeal: false
            );
        // Starlight end

        if (damageDealt == null)
            return;

        var damageEvent = new HitscanDamageDealtEvent
        {
            Target = args.Data.HitEntity.Value,
            DamageDealt = damageDealt,
            Data = args.Data, // Starlight
        };

        RaiseLocalEvent(ent, ref damageEvent);
    }
}
