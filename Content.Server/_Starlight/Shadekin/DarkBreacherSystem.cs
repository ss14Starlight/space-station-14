using Content.Server._Starlight.Shadekin.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class DarkBreacherSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _link = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ShadekinSystem _shadekin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkBreacherComponent, ChargedMachineActivatedEvent>(OnActivated);
        SubscribeLocalEvent<DarkBreacherComponent, OnAttemptPortalEvent>(OnAttemptPortal);
        SubscribeLocalEvent<DarkBreacherComponent, ChargedMachineDeactivatedEvent>((uid, _, _) => RemComp<LinkedEntityComponent>(uid));
    }

    private void OnAttemptPortal(EntityUid uid, DarkBreacherComponent component, OnAttemptPortalEvent args)
    {
        if (TryComp<PowerChargeComponent>(uid, out var power) && power.Active)
            return;

        args.Cancel();
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

    private EntityUid? GeneratePortal(DarkBreacherComponent component)
    {
        _shadekin.SpawnTheDark();
        // First lets find "The Dark".
        var query = EntityQueryEnumerator<DarkHubComponent>();
        while (query.MoveNext(out var target, out var portal))
            if (portal.Hub)
            {
                // We find "The Dark" or... at least "The Hub", If we have the hub but no dark you silly.
                var angle = _random.NextAngle();
                var location = angle.ToVec() * component.SpawnDistance;
                var position = _transform.GetWorldPosition(target) + location;
                var coords = new MapCoordinates(position, Transform(target).MapID);
                // Spawn it!
                return Spawn(component.Portal, coords);
            }

        return null;
    }
}
