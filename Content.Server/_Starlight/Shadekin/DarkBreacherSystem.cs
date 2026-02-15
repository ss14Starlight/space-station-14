using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Shadekin;

public sealed class DarkBreacherSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    private static readonly ProtoId<TagPrototype> _theDarkTag = "TheDark";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkBreacherComponent, ChargedMachineActivatedEvent>(OnActivated);
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

    private EntityUid? GeneratePortal(DarkBreacherComponent component)
    {
        SpawnTheDark();
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

    private void SpawnTheDark()
    {
        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapuid, out var mapcomp))
        {
            if (mapcomp.MapPaused)
                continue;

            if (_tag.HasTag(mapuid, _theDarkTag))
                return;
        }
        _gameTicker.StartGameRule("TheDarkMap");
    }
}
