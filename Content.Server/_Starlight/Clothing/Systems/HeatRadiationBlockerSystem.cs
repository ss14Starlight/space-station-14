using Content.Server._Starlight.Clothing.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared.Inventory;

namespace Content.Server._Starlight.Clothing.Systems;

public sealed class HeatRadiationBlockerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        Subs.SubscribeWithRelay<HeatRadiationBlockerComponent, RadiateHeatAttemptEvent>(OnRadiateHeatAttempt, held: false);
    }

    private void OnRadiateHeatAttempt(EntityUid uid, HeatRadiationBlockerComponent component, ref RadiateHeatAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
