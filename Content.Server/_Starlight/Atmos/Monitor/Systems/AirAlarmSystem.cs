//ReSharper disable CheckNamespace

using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared.Maps;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

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
        Tags = [ "Wall" ],
        Components = [
            "Firelock"
        ]
    };
    private readonly Dictionary<int,Vector2i> _offset = new()
    {
        [0] = new Vector2i(0,-1), //South
        [1] = new Vector2i(1,0), //East
        [2] = new Vector2i(0,1), //North
        [3] = new Vector2i(-1,0), //West
    };

    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    //for testing reasons
    private EntProtoId _lizardPlush = "PlushieLizard";

    private void SLInitialize()
    {
        SubscribeLocalEvent<AirAlarmComponent,GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<AirAlarmComponent> ent, ref GetVerbsEvent<Verb> ev)
    {
        var xform = Transform(ent);
        var offset = _offset[(int)(
            xform.LocalRotation / 90 //make it into a mutiple of 90
            % 4 //and then normalize to NSEW
        )];

        if (!_xform.TryGetMapOrGridCoordinates(ent, out var coords, xform))
            return; //no coords?

        var seen = new HashSet<Vector2i>();
        var start = coords.Value.ToVector2i(EntityManager, _xform) + offset;
        var work = new HashSet<Vector2i>
        {
            start
        };
        while (work.Count > 0)
        {
            var test = work.First();
            work.Remove(test);
            seen.Add(test);
            //and we do checking here
            var epos = new EntityCoordinates(ent, test);
            SpawnAtPosition(_lizardPlush, epos);

            var stop = _turf.GetEntitiesInTile(epos)
                .Select( x => _whitelist.IsWhitelistPass(_stoppingWhitelist, x))
                .Any(x => x);
            if (stop)
                continue;
            foreach (var direction in _offset.Values)
            {
                var newWork = test + direction;
                if (!seen.Contains(newWork))
                    work.Add(newWork);
            }
        }
        //xform.LocalRotation
        //xform.GridUid

    }
}
