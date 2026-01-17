using Content.Shared.Examine;

namespace Content.Shared._Starlight.FiringPins.Functionality;

public sealed partial class FiringPinShotCounterSystem : EntitySystem
{
    [Dependency] private readonly FiringPinSystem _firingPin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FiringPinShotCounterComponent, FiringPinFireAttemptEvent>(OnFireAttempt);
        SubscribeLocalEvent<FiringPinHolderComponent, ExaminedEvent>(OnHolderExamined);
    }

    private void OnFireAttempt(Entity<FiringPinShotCounterComponent> ent, ref FiringPinFireAttemptEvent args)
    {
        // we can ignore the failures for the most part, because if this is cancelled.. the pin won't be in the gun
        if(args.Cancelled) return;

        ent.Comp.ShotsFired++;
    }

    private void OnHolderExamined(Entity<FiringPinHolderComponent> ent, ref ExaminedEvent args)
    {
        if(!args.IsInDetailsRange) return;

        var firingPins = _firingPin.FiringPins(ent);
        foreach(var pin in firingPins)
        {
            if(TryComp<FiringPinShotCounterComponent>(pin, out var shotCounter))
            {
                args.PushMarkup(Loc.GetString("firing-pin-shotcounter-shots-fired", ("shots", shotCounter.ShotsFired)));
            }
        }
    }
}