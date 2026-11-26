// ReSharper disable CheckNamespace
using System.Linq;
using Content.Shared._Starlight.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;

namespace Content.Shared.Cuffs;

public abstract partial class SharedCuffableSystem
{
    /// <summary>
    /// Hands that cuffs can bind. Functional hands hold deployed limb items and aren't separate hands.
    /// </summary>
    public static int GetCuffableHandCount(HandsComponent hands) =>
        hands.Hands.Values.Count(hand => hand.Location != HandLocation.Functional);

    public bool IsCuffed(EntityUid target, bool requireFullyCuffed = true, CuffableComponent? cuffable = null) =>
        Resolve(target, ref cuffable) && IsCuffed((target, cuffable), requireFullyCuffed);

    private void OnAttackAttempt(EntityUid uid, CuffableComponent component, AttackAttemptEvent args)
    {
        if (component.CanStillInteract)
            return;

        var ev = new CuffedAttackAttemptEvent(args.Target, args.Weapon, args.Disarm);
        RaiseLocalEvent(uid, ref ev);

        if (!ev.Allowed)
            args.Cancel();
    }
}
