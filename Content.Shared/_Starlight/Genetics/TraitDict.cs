using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A thin wrapper around a dictionary with specific support for dictionary combining.
/// New dictionaries are constructed with the old dictionaries unmodified.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class TraitDict
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public Dictionary<ProtoId<GeneticTraitPrototype>, FixedPoint2> Traits = new();

    public TraitDict(Dictionary<ProtoId<GeneticTraitPrototype>, FixedPoint2> traits) => Traits = traits;

    public TraitDict() {}

    public static TraitDict Combine(IEnumerable<TraitDict> traitDicts)
    {
        var newDict = new TraitDict();
        foreach (var dict in traitDicts)
        {
            foreach (var tm in dict.Traits)
            {
                if (newDict.Traits.TryGetValue(tm.Key, out var value))
                    newDict.Traits[tm.Key] = value + tm.Value;
                else
                    newDict.Traits[tm.Key] = tm.Value;
            }
        }

        return newDict;
    }

    public static TraitDict Add(params TraitDict[] traitDicts) => Combine(traitDicts);
}
