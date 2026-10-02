using System.Linq;
using System.Text.Json.Serialization;
using Content.Shared._Starlight.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier : IEquatable<DamageSpecifier>, IRobustCloneable<DamageSpecifier>
{
    /// <summary>
    ///     Damage specified by group. Each value is divided evenly between the types in that group when applied.
    /// </summary>
    [JsonPropertyName("groups")]
    [DataField("groups")]
    private Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>? _damageGroupDict = new();

    [JsonIgnore]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> DamageGroupDict
    {
        get => _damageGroupDict ??= new();
        set => _damageGroupDict = value;
    }

    /// <summary>
    ///     A healing pool spent on the most damaged matching type first when this specifier is applied.
    /// </summary>
    [DataField("mixmax")]
    public DamageSpecifierMixMax? MixMax { get; set; }

    /// <summary>
    ///     Returns a copy with group damage divided between the concrete damage types in each group.
    /// </summary>
    public DamageSpecifier ResolveGroups(IPrototypeManager prototypeManager)
    {
        var resolved = new DamageSpecifier(this);
        resolved.DamageGroupDict.Clear();

        foreach (var (groupId, value) in DamageGroupDict)
        {
            if (!prototypeManager.TryIndex(groupId, out var group) || group.DamageTypes.Count == 0)
                continue;

            var remainingTypes = group.DamageTypes.Count;
            var remainingDamage = value;

            foreach (var damageType in group.DamageTypes)
            {
                var damage = remainingDamage / FixedPoint2.New(remainingTypes);
                if (!resolved.DamageDict.TryAdd(damageType, damage))
                    resolved.DamageDict[damageType] += damage;

                remainingDamage -= damage;
                remainingTypes--;
            }
        }

        return resolved;
    }
    private static void MergeMixMax(DamageSpecifier target, DamageSpecifier source, int sign)
    {
        if (source.MixMax == null)
            return;

        if (target.MixMax == null)
        {
            target.MixMax = source.MixMax.Clone();
            target.MixMax.Value *= sign;
            return;
        }

        target.MixMax.Value += source.MixMax.Value * sign;

        foreach (var group in source.MixMax.Groups)
        {
            if (!target.MixMax.Groups.Contains(group))
                target.MixMax.Groups.Add(group);
        }

        foreach (var type in source.MixMax.Types)
        {
            if (!target.MixMax.Types.Contains(type))
                target.MixMax.Types.Add(type);
        }
    }

    public FixedPoint2 this[string key] => DamageDict[key];
}
