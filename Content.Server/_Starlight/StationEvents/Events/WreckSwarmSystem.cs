using System.Numerics;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Salvage;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents.Events;

public sealed class WreckSwarmSystem : GameRuleSystem<WreckSwarmComponent>
{
    private readonly List<SalvageMapPrototype> _salvageMaps = new();

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Added(EntityUid uid, WreckSwarmComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
    }

    protected override void ActiveTick(EntityUid uid, WreckSwarmComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (_station.GetStations().Count == 0)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var station = RobustRandom.Pick(_station.GetStations());
        if (_station.GetLargestGrid(station) is not { } grid)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapId = Transform(grid).MapID;
        var playableArea = _physics.GetWorldAABB(grid);

        var minimumDistance = (playableArea.TopRight - playableArea.Center).Length() + 50f;
        var maximumDistance = minimumDistance + 100f;

        var center = playableArea.Center;

        var mapResource = SelectGrid(component);

        var angle = RobustRandom.NextAngle();
        var spawnAngle = RobustRandom.NextAngle();

        var offset = angle.RotateVec(new Vector2((maximumDistance - minimumDistance) * RobustRandom.NextFloat() + minimumDistance, 0));

        var spawnPosition = new MapCoordinates(center + offset, mapId);

        var wreckMap = _mapSystem.CreateMap();
        var wreckMapXform = Transform(wreckMap);
        if (
                !_loader.TryLoadGrid(wreckMapXform.MapID, mapResource, out _)
                || wreckMapXform.ChildCount == 0
                || !_mapSystem.TryGetMap(spawnPosition.MapId, out var spawnUid)
           )
        {
            // We couldn't load it, or it loaded empty - blame it on CC
            Announce(Loc.GetString("station-event-incoming-wreck-swarm-spawn-failed"), null);

            _mapSystem.DeleteMap(wreckMapXform.MapID);

            // Don't try to re-run
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapChildren = wreckMapXform.ChildEnumerator;

        // It worked, move it into position and cleanup values.
        while (mapChildren.MoveNext(out var mapChild))
        {
            var wreckXForm = Comp<TransformComponent>(mapChild);
            var localPos = wreckXForm.LocalPosition;

            _transform.SetParent(mapChild, wreckXForm, spawnUid.Value);
            _transform.SetWorldPositionRotation(mapChild, spawnPosition.Position + localPos, spawnAngle, wreckXForm);

            // We're using SetLinearVelocity because the map spawns in as if it's already moving
            var physics = Comp<PhysicsComponent>(mapChild);
            _physics.SetLinearVelocity(mapChild, -offset.Normalized() * component.Velocity, body: physics);
        }

        _mapSystem.DeleteMap(wreckMapXform.MapID);

        if (component.Announcement is { } locId)
            Announce(Loc.GetString(locId), component.AnnouncementSound);

        // Done processing, don't recur on next tick
        ForceEndSelf(uid, gameRule);
    }

    protected ResPath SelectGrid(WreckSwarmComponent component) {
        if (component.FixedGrid is not null) {
            return (ResPath)component.FixedGrid;
        } else {
            // Salvage map seed
            _salvageMaps.Clear();
            if (component.SizeFilter is not null) {
                _salvageMaps.AddRange(_proto.EnumeratePrototypes<SalvageMapPrototype>().Where((x) => x.SizeString.CompareTo((LocId)(component.SizeFilter)) == 0));
            } else {
                _salvageMaps.AddRange(_proto.EnumeratePrototypes<SalvageMapPrototype>());
            }
            _salvageMaps.Sort((x, y) => string.Compare(x.ID, y.ID, StringComparison.Ordinal));
            var map = RobustRandom.Pick(_salvageMaps);

            return map.MapPath;
        }
    }

    protected void Announce(string announcement, SoundSpecifier? sound) {
        // Let the players know (but we don't want to send to players who aren't in game (i.e. in the lobby))
        Filter allPlayersInGame = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

        _chat.DispatchFilteredAnnouncement(allPlayersInGame, announcement, playSound: false, colorOverride: Color.Gold);

        if (sound is not null) {
            _audio.PlayGlobal(sound, allPlayersInGame, true);
        }
    }
}
