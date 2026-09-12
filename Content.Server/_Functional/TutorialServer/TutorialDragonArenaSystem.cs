using System.Numerics;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Builds a cargo-bay box prey station plus a nearby space spawn for Space Dragon tutorials.
/// </summary>
public sealed partial class TutorialDragonArenaSystem : EntitySystem
{
    public const string StationApproachMarkerId = "dragon-station";

    private static readonly EntProtoId StepMarkerProto = "TutorialStepMarker";
    private static readonly EntProtoId PinpointerProto = "TutorialPinpointerDragonStation";

    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialPracticeRoomSystem _rooms = default!;
    [Dependency] private TutorialShuttleArenaSystem _shuttleArenas = default!;

    public bool TryBuildArena(
        ProtoId<TutorialDragonArenaPrototype> arenaId,
        out EntityUid mapUid,
        out EntityUid stationUid,
        out EntityCoordinates spawnCoords)
    {
        mapUid = EntityUid.Invalid;
        stationUid = EntityUid.Invalid;
        spawnCoords = default;

        if (!_protos.TryIndex(arenaId, out TutorialDragonArenaPrototype? arena))
        {
            Log.Error($"Unknown tutorialDragonArena {arenaId}");
            return false;
        }

        mapUid = _map.CreateMap(out var mapId);

        stationUid = _shuttleArenas.BuildCargoBayBoxStation(
            mapId,
            arena.DockProto,
            arena.StationId,
            attachTradeStation: false);
        _shuttleArenas.PrepareDockStationGrid(stationUid);
        _rooms.EnsureBreathableAtmosphere(stationUid);
        _transform.SetMapCoordinates(stationUid, new MapCoordinates(arena.StationOffset, mapId));

        // One chamber so practiceSpawns land on the bay, not at the space spawn.
        var layout = EnsureComp<TutorialRoomLayoutComponent>(stationUid);
        layout.ChamberCenters.Clear();
        layout.ChamberCenters.Add(new Vector2(7.5f, 5.5f));
        layout.GateDoors.Clear();
        Dirty(stationUid, layout);

        // Pinpointer lock-on target (center of bay).
        var beacon = Spawn(StepMarkerProto, new EntityCoordinates(stationUid, new Vector2(7.5f, 5.5f)));
        EnsureComp<TutorialDragonPreyBeaconComponent>(beacon);

        var approach = Spawn(StepMarkerProto, new EntityCoordinates(stationUid, new Vector2(5.5f, 5.5f)));
        var approachMarker = EnsureComp<TutorialStepMarkerComponent>(approach);
        approachMarker.MarkerId = StationApproachMarkerId;
        Dirty(approach, approachMarker);

        var spacePos = arena.StationOffset + arena.SpaceSpawnOffset;
        spawnCoords = new EntityCoordinates(mapUid, spacePos);

        var pinPos = spacePos + arena.PinpointerOffset;
        var pin = Spawn(PinpointerProto, new EntityCoordinates(mapUid, pinPos));
        if (TryComp<PinpointerComponent>(pin, out var pinComp))
        {
            _pinpointer.SetTarget(pin, beacon); // Starlight edit
            _pinpointer.SetActive(pin, true); // Starlight edit
        }

        Log.Info($"TUTORIAL_E2E: dragon_arena_ready station={stationUid} spawn={spacePos}");
        return true;
    }
}
