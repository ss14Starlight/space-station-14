using Content.Shared._Starlight.Shadekin;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Teleportation.Components;

namespace Content.Client._Starlight.Shadekin;

public sealed class DarkPortalSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkPortalComponent, OnAttemptPortalEvent>(OnAttemptPortal);
    }

    // APPRENTLY... MOVING THIS TO SHARED IS NOT TRIGGERED? SO I HAVE TO FUCKING COPY/PASTE ON CLIENT? WTF?
    private void OnAttemptPortal(EntityUid uid, DarkPortalComponent component, OnAttemptPortalEvent args)
    {
        if (HasComp<BrighteyeComponent>(args.Subject))
            return;

        // TODO: Check if we have the Nullspace Suit?

        if (TryComp<PullableComponent>(args.Subject, out var pullablea) && pullablea.BeingPulled && HasComp<BrighteyeComponent>(pullablea.Puller))
            return;

        args.Cancel();
    }
}
