using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits;

/// <summary>
/// Order traits are validated in: cheapest first, so point-granting traits are counted before the traits they pay for.
/// </summary>
public static class TraitOrdering
{
    public static IOrderedEnumerable<T> OrderForValidation<T>(
        IEnumerable<T> source,
        Func<T, ProtoId<TraitPrototype>> idSelector,
        IPrototypeManager protoManager)
    {
        return source
            .OrderBy(x => protoManager.TryIndex(idSelector(x), out var proto) ? proto.Cost : 0)
            .ThenBy(x => idSelector(x).Id, StringComparer.Ordinal);
    }

    public static IOrderedEnumerable<ProtoId<TraitPrototype>> OrderForValidation(
        IEnumerable<ProtoId<TraitPrototype>> traits,
        IPrototypeManager protoManager)
    {
        return OrderForValidation(traits, id => id, protoManager);
    }
}
