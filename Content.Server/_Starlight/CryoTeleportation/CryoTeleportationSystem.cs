using Content.Server.Bed.Cryostorage;
using Content.Shared._Starlight.Polymorph.Components;
using Content.Server.Station.Systems;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking;
using Content.Shared._Starlight.CryoTeleportation;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.CryoTeleportation;

public sealed partial class CryoTeleportationSystem : EntitySystem
{
    [Dependency] private CryostorageSystem _cryostorage = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public TimeSpan _nextTick = TimeSpan.Zero;
    private readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnCompleteSpawn);
        SubscribeLocalEvent<TargetCryoTeleportationComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<TargetCryoTeleportationComponent, PlayerAttachedEvent>(OnPlayerAttached);
        _playerMan.PlayerStatusChanged += OnSessionStatus;
    }

    public override void Update(float delay)
    {
        if (_nextTick > _timing.CurTime)
            return;

        _nextTick += _refreshCooldown;

        var query = AllEntityQuery<TargetCryoTeleportationComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobStateComponent))
        {
            if (comp.Station == null
                || !TryComp<StationCryoTeleportationComponent>(comp.Station, out var stationComp)
                || !TryComp<StationDataComponent>(comp.Station, out var stationData)
                || HasComp<BorgChassisComponent>(uid)
                || mobStateComponent.CurrentState != MobState.Alive
                || comp.ExitTime == null
                || _timing.CurTime - comp.ExitTime - comp.TimeDelay < stationComp.TransferDelay
                || HasComp<CryostorageContainedComponent>(uid)
                || HasComp<UncryoableComponent>(uid))
                continue;

            var stationGrid = _stationSystem.GetLargestGrid((comp.Station.Value, stationData));

            if (stationGrid == null)
                continue;

            var cryoStorageResult = FindCryoStorage(comp.Station.Value);

            if (cryoStorageResult == null)
                continue;

            var (cryoStorage, container) = cryoStorageResult.Value;

            var containedComp = AddComp<CryostorageContainedComponent>(uid);

            containedComp.Cryostorage = cryoStorage;
            containedComp.GracePeriodEndTime = _timing.CurTime;

            var portalCoordinates = _transformSystem.GetMapCoordinates(Transform(uid));

            var portalUid = _entity.SpawnEntity(stationComp.PortalPrototype, portalCoordinates);
            _audio.PlayPvs(stationComp.TransferSound, portalUid);

            if (!_container.Insert(uid, container))
                _cryostorage.HandleEnterCryostorage((uid, containedComp), comp.UserId);
        }
    }

    private void OnCompleteSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<StationCryoTeleportationComponent>(ev.Station)
            || ev.JobId == null
            || ev.Player.AttachedEntity == null
            || !_configurationManager.GetCVar(StarlightCCVars.CryoTeleportation))
            return;

        var targetComponent = EnsureComp<TargetCryoTeleportationComponent>(ev.Player.AttachedEntity.Value);
        targetComponent.Station = ev.Station;
        targetComponent.UserId = ev.Player.UserId;
    }

    private void OnPlayerDetached(EntityUid uid, TargetCryoTeleportationComponent comp, PlayerDetachedEvent ev)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || mobStateComponent.CurrentState != MobState.Alive)
            return;
        if (comp.ExitTime == null)
            comp.ExitTime = _timing.CurTime;
        if (_mind.TryGetMind(uid, out var _, out var mind))
            comp.UserId = mind.UserId;
    }

    private void OnPlayerAttached(EntityUid uid, TargetCryoTeleportationComponent comp, PlayerAttachedEvent ev)
    {
        if (comp.ExitTime != null)
            comp.ExitTime = null;
        if (_mind.TryGetMind(uid, out var _, out var mind))
            comp.UserId = mind.UserId;
    }

    private void OnSessionStatus(object? sender, SessionStatusEventArgs args)
    {
        if (!TryComp<TargetCryoTeleportationComponent>(args.Session.AttachedEntity, out var comp))
            return;

        if (args.Session.Status == SessionStatus.Disconnected && comp.ExitTime == null)
            comp.ExitTime = _timing.CurTime;
        else if (args.Session.Status == SessionStatus.Connected && comp.ExitTime != null)
            comp.ExitTime = null;

        comp.UserId = args.Session.UserId;
    }

    /// <summary>
    /// Finds an non-occupied cryo storage unit on the station's main grid.
    /// </summary>
    /// <param name="stationUid">station to be searched for cryo storage</param>
    /// <returns>
    /// An available cryo storage unit and its container slot, or null if no available cryo units were found.
    /// Cryo units that are not on the station's main grid will not be returned, avoiding selecting cryo units off-station,
    /// such as on the ATS, shuttles, etc., which may be unsafe for storing people or annoying for people to retrieve job equipment.
    /// </returns>
    private (EntityUid Uid, ContainerSlot Container)? FindCryoStorage(EntityUid stationUid)
    {
        var stationData = EntityManager.GetComponentOrNull<StationDataComponent>(stationUid);

        // if the whole station is gone, we're not putting anyone into cryo storage anyway
        if(stationData == null)
            return null;

        // main station grid check. if the main grids is (somehow) empty, fallbackt o any grid in station data
        var grids = stationData.MainGrids.Count > 0
            ? stationData.MainGrids
            : stationData.Grids;

        var query = AllEntityQuery<CryostorageComponent, TransformComponent>();
        while (query.MoveNext(out var cryoUid, out _, out var cryoTransform))
        {
            if (cryoTransform.GridUid is not { } gridUid)
                continue;

            // skip any cryo storage that is not on the main station grids
            if(!grids.Contains(gridUid))
                continue;

            var container = _container.EnsureContainer<ContainerSlot>(cryoUid, "storage");

            if (container.ContainedEntities.Count > 0)
                continue;

            return (cryoUid, container);
        }

        // if we couldn't find a cryo storage unit, return null
        return null;
    }
}
