using System.Linq;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    /// <summary>
    /// Resolves data-only damage instructions into the concrete damage types stored by a damageable component.
    /// Mix-max healing is not meaningful for stored absolute damage, so it is discarded here.
    /// </summary>
    private DamageSpecifier ResolveStoredDamage(DamageSpecifier damage)
    {
        if (damage.DamageGroupDict.Count != 0)
            damage = damage.ResolveGroups(_prototypeManager);

        if (damage.MixMax == null)
            return damage;

        damage = new DamageSpecifier(damage)
        {
            MixMax = null,
        };
        return damage;
    }

    /// <summary>
    /// Resolves group damage and converts mix-max healing into ordinary per-type healing for this target.
    /// </summary>
    private DamageSpecifier ResolveDamageChange(Entity<DamageableComponent> ent, DamageSpecifier damage)
    {
        if (damage.DamageGroupDict.Count != 0)
            damage = damage.ResolveGroups(_prototypeManager);

        if (damage.MixMax == null)
            return damage;

        var mixMax = damage.MixMax;
        var resolved = new DamageSpecifier(damage)
        {
            MixMax = null,
        };

        if (mixMax.Value >= FixedPoint2.Zero)
            return resolved;

        var candidateTypes = new HashSet<ProtoId<DamageTypePrototype>>();
        foreach (var groupId in mixMax.Groups)
        {
            if (!_prototypeManager.TryIndex(groupId, out var group))
                continue;

            foreach (var type in group.DamageTypes)
            {
                candidateTypes.Add(type);
            }
        }

        foreach (var type in mixMax.Types)
        {
            candidateTypes.Add(type);
        }

        var candidates = new List<(ProtoId<DamageTypePrototype> Type, FixedPoint2 Damage)>();
        foreach (var type in candidateTypes)
        {
            if (!SupportsType(ent.Comp.DamageContainerID, type))
                continue;

            var current = ent.Comp.Damage.DamageDict.GetValueOrDefault(type);
            var pending = resolved.DamageDict.GetValueOrDefault(type);
            var available = FixedPoint2.Max(FixedPoint2.Zero, current + pending);
            if (available > FixedPoint2.Zero)
                candidates.Add((type, available));
        }

        var remaining = -mixMax.Value;
        foreach (var (type, available) in candidates
                    .OrderByDescending(candidate => candidate.Damage)
                    .ThenBy(candidate => candidate.Type.Id, StringComparer.Ordinal))
        {
            var healing = FixedPoint2.Min(available, remaining);
            if (!resolved.DamageDict.TryAdd(type, -healing))
                resolved.DamageDict[type] -= healing;

            remaining -= healing;
            if (remaining <= FixedPoint2.Zero)
                break;
        }

        return resolved;
    }

    public void ClearAllDamage(Entity<DamageableComponent?> ent) =>
        SetAllDamage(ent, FixedPoint2.Zero);

    // Begin Stellar - We need to be able to change DamageContainer to make cultists vulnerable to Holy Damage
    public void SetDamageContainerID(Entity<DamageableComponent?> ent, ProtoId<DamageContainerPrototype>? damageContainerId)
    {
        if (!_damageableQuery.Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.DamageContainerID = damageContainerId;
        Dirty(ent);
    }
    // End Stellar

    /// <summary>
    ///     Adds to the additive damage modifiers of the DamageableComponent
    /// </summary>
    public void AddAdditiveModifierSet(Entity<DamageableComponent?> ent, EntityUid source, DamageModifierSet mods)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;

        foreach (var coefficient in mods.Coefficients)
            ent.Comp.AdditiveCoefficients.TryAdd((source, coefficient.Key), coefficient.Value);

        foreach (var modifier in mods.FlatReductions)
            ent.Comp.AdditiveModifiers.TryAdd((source, modifier.Key), modifier.Value);
    }

    /// <summary>
    ///     Subtracts from the additive damage modifiers of the DamageableComponent
    /// </summary>
    public void RemoveAdditiveModifierSet(Entity<DamageableComponent?> ent, EntityUid source, DamageModifierSet mods)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;

        foreach (var coefficient in mods.Coefficients)
            ent.Comp.AdditiveCoefficients.Remove((source, coefficient.Key));

        foreach (var modifier in mods.FlatReductions)
            ent.Comp.AdditiveModifiers.Remove((source, modifier.Key));
    }
}
