// ReSharper disable CheckNamespace
using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;

namespace Content.Shared.Cuffs;

public abstract partial class SharedCuffableSystem
{
    /// <summary>
    /// Hands that cuffs can bind. Functional hands hold deployed limb items and aren't separate hands.
    /// </summary>
    public static int GetCuffableHandCount(HandsComponent hands) =>
        hands.Hands.Values.Count(hand => hand.Location != HandLocation.Functional);

    public bool IsCuffed(EntityUid target, bool requireFullyCuffed = true, CuffableComponent? cuffable = null) =>
        Resolve(target, ref cuffable) && IsCuffed((target, cuffable));
}
