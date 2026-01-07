using Content.Server.Power.Components;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server._Starlight.Holograms;

public sealed class HologramProjectorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent((EntityUid ent, HologramProjectorComponent comp, ref PowerChangedEvent _) => CheckState(ent, comp));
        SubscribeLocalEvent<HologramProjectorComponent, MapInitEvent>((ent, comp, args) => CheckState(ent, comp));
    }

    public void CheckState(EntityUid projector, HologramProjectorComponent? projComp = null)
    {
        if (!Resolve(projector, ref projComp))
            return;

        var shouldBeActive = !((TryComp<ApcPowerReceiverComponent>(projector, out var powerComp) && !powerComp.Powered) ||
            (TryComp<SurveillanceCameraComponent>(projector, out var cameraComp) && !cameraComp.Active));

        if (projComp.IsActive != shouldBeActive)
        {
            projComp.IsActive = shouldBeActive;
            Dirty(projector, projComp);
        }
    }
}
