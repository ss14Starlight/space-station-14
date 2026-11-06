using System.Linq;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Damage;

/// <summary>
/// Describes a healing pool that is spent on the most damaged matching damage types first.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DamageSpecifierMixMax : IEquatable<DamageSpecifierMixMax>
{
    /// <summary>
    /// The total amount of damage to heal across all matching types.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Value;

    /// <summary>
    /// Damage groups whose types may be healed.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageGroupPrototype>> Groups = new();

    /// <summary>
    /// Individual damage types that may be healed.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> Types = new();

    public DamageSpecifierMixMax Clone() => new DamageSpecifierMixMax
    {
        Value = Value,
        Groups = new List<ProtoId<DamageGroupPrototype>>(Groups),
        Types = new List<ProtoId<DamageTypePrototype>>(Types),
    };

    public bool Equals(DamageSpecifierMixMax? other) => other != null &&
                Value == other.Value &&
                Groups.SequenceEqual(other.Groups) &&
                Types.SequenceEqual(other.Types);

    public override bool Equals(object? obj)
    {
        return obj is DamageSpecifierMixMax other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Value);

        foreach (var group in Groups)
        {
            hash.Add(group);
        }

        foreach (var type in Types)
        {
            hash.Add(type);
        }

        return hash.ToHashCode();
    }
}
