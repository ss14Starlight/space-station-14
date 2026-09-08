using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Shadekin;

public sealed class DarkBreacherSystem : SharedDarkBreacherSystem
{
    [Dependency] private LinkedEntitySystem _link = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkBreacherComponent, ChargedMachineActivatedEvent>(OnActivated);
        SubscribeLocalEvent<DarkBreacherComponent, OnAttemptPortalEvent>(OnAttemptPortal);
        SubscribeLocalEvent<DarkBreacherComponent, ChargedMachineDeactivatedEvent>((uid, _, _) => RemComp<LinkedEntityComponent>(uid));
    }

    private void OnActivated(Entity<DarkBreacherComponent> ent, ref ChargedMachineActivatedEvent args)
    {
        var query = EntityQueryEnumerator<DarkHubComponent>();
        while (query.MoveNext(out var target, out var portal))
            if (!portal.Hub)
            {
                _link.TryLink(ent.Owner, target);
                return;
            }

        // Ohoh! There is no Non-Hub Portal! Lets Generate one!
        var newportal = GeneratePortal(ent.Comp);
        if (newportal is not null)
            _link.TryLink(ent.Owner, newportal.Value);
    }

    private void OnAttemptPortal(EntityUid uid, DarkBreacherComponent component, OnAttemptPortalEvent args)
    {
        if (TryComp<PowerChargeComponent>(uid, out var power) && power.Active)
            return;

        args.Cancel();
    }
}
