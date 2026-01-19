using Content.Shared.Station;

namespace Content.Shared._Starlight.FiringPins.Functionality;

public sealed partial class FiringPinExpeditionSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FiringPinExpeditionComponent, FiringPinFireAttemptEvent>(OnFireAttempt);
    }

    private bool CanFire(Entity<FiringPinExpeditionComponent> ent) => _station.GetOwningStation(ent.Owner) == null;

    private void OnFireAttempt(Entity<FiringPinExpeditionComponent> ent, ref FiringPinFireAttemptEvent args) => args.Cancelled = !CanFire(ent) ? true : args.Cancelled;
}