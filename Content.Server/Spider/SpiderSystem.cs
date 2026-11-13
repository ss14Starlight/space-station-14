using System.Linq;
using Content.Server.Popups;
using Content.Shared.Spider;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

#region Starlight
using Content.Shared._Starlight.Spider.Events;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;
#endregion

namespace Content.Server.Spider;

public sealed partial class SpiderSystem : SharedSpiderSystem
{
    [Dependency] private PopupSystem _popup = default!;
    //[Dependency] private TurfSystem _turf = default!; // Starlight-removed - we dropped the one use of this system
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    ///     A recycled hashset used to check turfs for spiderwebs.
    /// </summary>
    private readonly HashSet<EntityUid> _webs = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderComponent, SpiderWebActionEvent>(OnSpawnNet);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpiderComponent>();
        while (query.MoveNext(out var uid, out var spider))
        {
            spider.NextWebSpawn ??= _timing.CurTime + spider.WebSpawnCooldown;

            if (_timing.CurTime < spider.NextWebSpawn)
                continue;

            spider.NextWebSpawn += spider.WebSpawnCooldown;

            if (HasComp<ActorComponent>(uid)
                || _mobState.IsDead(uid)
                || !spider.SpawnsWebsAsNonPlayer)
                continue;

            var transform = Transform(uid);
            SpawnWeb((uid, spider), transform.Coordinates);
        }
    }

    private void OnSpawnNet(EntityUid uid, SpiderComponent component, SpiderWebActionEvent args)
    {
        if (args.Handled)
            return;

        // Starlight-start
        if (_container.IsEntityInContainer(uid))
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-incontainer"), args.Performer, args.Performer);
            return;
        }
        // Starlight-end

        var transform = Transform(uid);

        if (transform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-nogrid"), args.Performer, args.Performer);
            return;
        }

        var result = SpawnWeb((uid, component), transform.Coordinates);

        if (result)
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-success"), args.Performer, args.Performer);
            args.Handled = true;
        }
        else if (!component.OneWebSpawn)
            _popup.PopupEntity(Loc.GetString("spider-web-action-fail"), args.Performer, args.Performer);
        else
            _popup.PopupEntity(Loc.GetString("spider-web-action-fail-single"), args.Performer, args.Performer);
    }

    private bool SpawnWeb(Entity<SpiderComponent> ent, EntityCoordinates coords)
    {
        var result = false;
        var shouldRunEvent = true; // Starlight-edit

        // Spawn web in center
        if (!IsTileBlockedByWeb(coords, out var web))
        {
            Spawn(ent.Comp.WebPrototype, coords);
            result = true;
        }
        // Starlight-start
        else if (ent.Comp.ReplacementAllowed && web.Value.Comp.Replaceable)
        {
            QueueDel(web.Value);
            SpawnAtPosition(ent.Comp.WebPrototype, coords);
            // Don't run spawn event because it's replacement. So we don't add it to progress in evolution system. If you reading this and anyway want to process replacement - create another event and raise it if it replaces web.
            shouldRunEvent = false;
            result = true;
        }
        // Starlight-end

        SLSpawnWeb(ent, coords, ref shouldRunEvent, ref result); // Starlight-edit

        return result;
    }

}
