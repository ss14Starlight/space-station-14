using Content.Shared._Starlight.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;

namespace Content.Shared.Movement.Pulling.Systems;

public sealed partial class PullingSystem
{
    private void InitializeTrain()
        => SubscribeLocalEvent<PullTrainCarComponent, PullStoppedMessage>(OnCarPullStopped);

    /// <summary>
    /// Hooks a new car in behind the head of the train so the existing cars trail off it rather than being dropped.
    /// Targeting a car that is already coupled uncouples it, and everything behind it, instead.
    /// </summary>
    /// <returns>Null if the head should just pull <paramref name="pullableUid"/> the normal way.</returns>
    private bool? CoupleToTrain(EntityUid headUid, PullerComponent headComp, PullTrainComponent train, EntityUid pullableUid)
    {
        if (headComp.Pulling is not { } first)
            return null;

        var tail = headUid;
        var cars = 0;

        while (cars <= train.MaxCars && TryComp<PullerComponent>(tail, out var link) && link.Pulling is { } next)
        {
            if (next == pullableUid)
                return TryComp<PullableComponent>(next, out var coupled) && TryStopPull(next, coupled, headUid);

            tail = next;
            cars++;
        }

        if (cars >= train.MaxCars)
        {
            _popup.PopupEntity(Loc.GetString("pulling-train-full"), headUid, headUid);
            return false;
        }

        if (!TryComp<PullableComponent>(first, out var firstPullable))
            return null;

        var wasCar = HasComp<PullTrainCarComponent>(first);
        if (wasCar)
            RemComp<PullTrainCarComponent>(first);

        var granted = !HasComp<PullerComponent>(pullableUid);
        var carPuller = EnsureComp<PullerComponent>(pullableUid);

        if (granted)
        {
            carPuller.NeedsHands = false;
            Dirty(pullableUid, carPuller);
        }

        if (TryStartPull(pullableUid, first, carPuller, firstPullable))
        {
            if (granted)
                EnsureComp<PullTrainCarComponent>(pullableUid);

            return null;
        }

        if (granted)
            RemComp<PullerComponent>(pullableUid);

        if (wasCar)
            EnsureComp<PullTrainCarComponent>(first);

        return false;
    }

    private void OnCarPullStopped(Entity<PullTrainCarComponent> ent, ref PullStoppedMessage args)
    {
        // Only react to the car itself being uncoupled, not to it letting go of the car behind it.
        if (args.PulledUid != ent.Owner)
            return;

        if (TryComp<PullerComponent>(ent, out var puller)
            && puller.Pulling is { } next
            && TryComp<PullableComponent>(next, out var nextPullable)
            && !TryStopPull(next, nextPullable))
        {
            return;
        }

        RemComp<PullerComponent>(ent);
        RemComp<PullTrainCarComponent>(ent);
    }
}
