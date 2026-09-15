using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private DamageExamineSystem _damageExamine = default!;

    // needed for server system
    protected virtual void InitializeCartridge()
    {
        SubscribeLocalEvent<CartridgeAmmoComponent, ExaminedEvent>(OnCartridgeExamine);
        SubscribeLocalEvent<CartridgeAmmoComponent, DamageExamineEvent>(OnCartridgeDamageExamine);
    }

    private void OnCartridgeExamine(Entity<CartridgeAmmoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Spent
            ? Loc.GetString("gun-cartridge-spent")
            : Loc.GetString("gun-cartridge-unspent"));
    }

    #region Starlight
    // starlight - support hitscan projectiles for damage detail viewing

    private void OnCartridgeDamageExamine(Entity<CartridgeAmmoComponent> ent, ref DamageExamineEvent args)
    {
        if(GetProjectileDamage(ent.Comp.Prototype) is not (DamageSpecifier, float) damageSpec)
            return;

        _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec.Item1), Loc.GetString("damage-projectile"));

        // show armor penetration for hitscan, but only if its actually nonzero
        if (damageSpec.Item2 != 0f)
        {
            var msg = new FormattedMessage();
            var loc = damageSpec.Item2 < 0 ? "damage-examine-penetration-negative" : "damage-examine-penetration-positive";
            var pen = Math.Abs(Math.Round(damageSpec.Item2 * 100f));
            if (msg.TryAddMarkup(Loc.GetString(loc, ("penetration", pen)), out _))
            {
                args.Message.PushNewline();
                args.Message.AddMessage(msg);
            }
        }
    }

    private (DamageSpecifier, float)? GetProjectileDamage(EntProtoId proto)
    {
        if (!ProtoManager.TryIndex(proto, out var entityProto))
            return null;

        if (entityProto.TryGetComponent<ProjectileComponent>(out var projectile, Factory))
        {
            if (!projectile.Damage.Empty)
                return (projectile.Damage * Damageable.UniversalProjectileDamageModifier, 0f);
        }
        else if (entityProto.TryGetComponent<HitscanBasicDamageComponent>(out var hitscan, Factory))
        {
            if (!hitscan.Damage.Empty)
            {
                float hitscanPenetration = hitscan.IgnoreResistances ? 1f : hitscan.ArmorPenetration;
                return (hitscan.Damage * Damageable.UniversalHitscanDamageModifier, hitscanPenetration);
            }
        }

        return null;
    }
    #endregion
}
