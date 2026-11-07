using Content.Shared.Bed.Sleep;
using Content.Shared.Nutrition.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class HungerSystem
{
    private const float SleepingDecayMultiplier = 1f / 3f;

    [SubscribeLocalEvent]
    private void OnSleepStateChanged(Entity<HungerComponent> ent, ref SleepStateChangedEvent args)
        => SetAuthoritativeHungerValue(ent, GetHunger(ent.Comp, !args.FellAsleep));

    private float GetSleepingDecayMultiplier(EntityUid uid) =>
        HasComp<SleepingComponent>(uid) ? SleepingDecayMultiplier : 1f;
}
