//ReSharper disable CheckNamespace

using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared.Maps;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.Monitor.Systems;

public sealed partial class AirAlarmSystem
{
    /// <summary>
    /// when encountering a entity on a tile add it to the links for a air alarm
    /// </summary>
    private readonly EntityWhitelist _linkableWhitelist = new()
    {
        Tags = new(),
        Components = [
            "AtmosMonitor",
            "AtmosAlarmable"
        ]
    };
    /// <summary>
    /// when a tile has a entity tagged with this on it. do not queue adjacent tiles.
    /// </summary>
    private readonly EntityWhitelist _stoppingWhitelist = new()
    {
        Tags = ["Wall"],
        Components = [
            "Firelock"
        ]
    };
    private readonly Dictionary<int, Vector2i> _offset = new()
    {
        [0] = new Vector2i(0, -1), //South
        [1] = new Vector2i(1, 0), //East
        [2] = new Vector2i(0, 1), //North
        [3] = new Vector2i(-1, 0), //West
    };

    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    //for testing reasons
    private EntProtoId _lizardPlush = "PlushieLizard";

    private int MaxDepth = 10;
    private void SLInitialize()
    {
        SubscribeLocalEvent<AirAlarmComponent, GetVerbsEvent<Verb>>(OnGetVerbs);


    }

    private void OnGetVerbs(Entity<AirAlarmComponent> ent, ref GetVerbsEvent<Verb> ev) => ev.Verbs.Add(new Verb()
    {
        Text = Loc.GetString("admin-verb-autolink"),
        Icon = new SpriteSpecifier.Rsi(new("/Textures/Structures/Wallmounts/air_monitors.rsi"), "alarmp"),
        Category = VerbCategory.Tricks,
        Act = () =>
        {
            var xform = Transform(ent);
            var offset = _offset[(int)(
               xform.LocalRotation.Degrees / 90 //make it into a mutiple of 90
               % 4 //and then normalize to NSEW
            )];

            if (!_xform.TryGetMapOrGridCoordinates(ent, out var coords, xform))
                return; //no coords?

            var seen = new HashSet<EntityCoordinates>();
            var start = coords.Value.Offset(offset);
            var work = new HashSet<(EntityCoordinates,int)>
            {
                (start,0)
            };
            Log.Info($"starting at {start}");
            while (work.Count > 0)
            {
                var part = work.First();
                var test = part.Item1;
                work.Remove(part);
                seen.Add(test);
                //and we do checking here
                Log.Info($"working on {test}, depth {part.Item2} ({work.Count} to process)");
                SpawnAtPosition(_lizardPlush, test);

                var tref = _turf.GetTileRef(test);
                var stop = _turf.GetEntitiesInTile(test)
                   .Select(x => _whitelist.IsWhitelistPass(_stoppingWhitelist, x))
                   .Where(x => _turf.GetTileRef(_xform.GetGridTilePositionOrDefault(x)))
                   .Any(x => x);
                if (stop || part.Item2 > MaxDepth)
                {
                    Log.Info($"Stopping, {stop} depth: {part.Item2}");
                    continue;
                }
                foreach (var direction in _offset.Values)
                {
                    var newWork = test.Offset(direction);
                    if (!seen.Contains(newWork))
                        work.Add((newWork,part.Item2+2));
                }
            }
            //xform.LocalRotation
            //xform.GridUid
        }
    });


}
