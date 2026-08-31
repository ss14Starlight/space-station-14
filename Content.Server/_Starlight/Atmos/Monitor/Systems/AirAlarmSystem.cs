//ReSharper disable CheckNamespace

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Maps;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
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
        Components = [
            "Firelock",
            "Airtight"
        ]
    };
    private readonly Dictionary<int, Vector2i> _offset = new()
    {
        [0] = Vector2i.Down, //South
        [1] = Vector2i.Right, //East
        [2] = Vector2i.Up, //North
        [3] = Vector2i.Left, //West
    };

    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    //for testing reasons
    // private EntProtoId _lizardPlush = "PlushieLizard";

    private int _maxDepth = 10;
    private void SLInitialize()
    {
        SubscribeLocalEvent<AirAlarmComponent, GetVerbsEvent<Verb>>(OnGetVerbs);

        _configuration.OnValueChanged(StarlightCCVars.MaxAutoAtmosLinkDistance, x => _maxDepth = x);
    }

    private void OnGetVerbs(Entity<AirAlarmComponent> ent, ref GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Mapping))
            return;
        ev.Verbs.Add(new Verb()
        {
            Text = Loc.GetString("admin-verb-autolink"),
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Structures/Wallmounts/air_monitors.rsi"), "alarmp"),
            Category = VerbCategory.Tricks,
            Act = () =>
            {
                if (!TryComp<DeviceListComponent>(ent, out var devList))
                    return;

                var xform = Transform(ent);
                var offset = _offset[(int)(
                    xform.LocalRotation.Degrees / 90 //make it into a mutiple of 90
                    % 4 //and then normalize to NSEW
                )];

                if (!_xform.TryGetMapOrGridCoordinates(ent, out var coords, xform))
                    return; //no coords?

                var seen = new HashSet<EntityCoordinates>();
                var start = coords.Value.Offset(offset);
                var work = new HashSet<(EntityCoordinates, int)>
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
                    // SpawnAtPosition(_lizardPlush, test);

                    var tref = _turf.GetTileRef(test);
                    if (tref == null)
                        continue; //in space. so we dont continue propogation
                    var stop = _turf.GetEntitiesInTile(test, LookupFlags.StaticSundries)
                        .Where(x => //Filter for entities on the tile we are checking (stupid walls are just slightly too fat)
                        {
                            var tile = _xform.GetGridTilePositionOrDefault(x);
                            return tile != Vector2i.Zero && tref.Value.GridIndices == tile;
                        })
                        .Select(x =>
                        {
                            if (_whitelist.IsWhitelistPass(_linkableWhitelist, x) && TryComp<DeviceNetworkComponent>(x, out var devNetwork))
                                _deviceList.TryAddDeviceToList(
                                new(ent, devList),
                                new(x, devNetwork)
                            );
                            Log.Info($"euid: {x} WLPass: {_whitelist.IsWhitelistPass(_linkableWhitelist, x)} DNComp: {HasComp<DeviceNetworkComponent>(x)}");
                            return _whitelist.IsWhitelistPass(_stoppingWhitelist, x);
                        })
                        .Any(x => x);

                    if (stop || part.Item2 > _maxDepth)
                        continue;

                    foreach (var direction in _offset.Values)
                    {
                        var newWork = test.Offset(direction);
                        if (!seen.Contains(newWork))
                            work.Add((newWork, part.Item2 + 2));
                    }
                }
                //xform.LocalRotation
                //xform.GridUid
            }
        });
    }
}
