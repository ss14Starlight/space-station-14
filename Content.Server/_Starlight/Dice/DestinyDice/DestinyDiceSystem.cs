using System.Linq;
using Content.Server.Administration.Toolshed;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Nuke;
using Content.Server.Tabletop;
using Content.Server.Tabletop.Components;
using Content.Shared._Starlight.Dice.DestinyDice;
using Content.Shared._Starlight.Dice;
using Content.Shared.Atmos;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Dice;
using Content.Shared.Explosion;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nuke;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Sprite;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.Tabletop.Components;
using Content.Shared.Throwing;
using Content.Shared.Tiles;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Dice.DestinyDice;

public sealed class DestinyDiceSystem : SharedDestinyDiceSystem
{
    [Dependency] private readonly TabletopSystem _tabletop = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly TileSystem _tiles = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    
    /// <summary>
    /// All pending effect groups. Cleared on round restart.
    /// </summary>
    private readonly List<PendingDestinyDiceEffectGroup> _pendingEffectGroups = [];
    
    /// <summary>
    /// All pending effects. Cleared on round restart.
    /// </summary>
    private readonly List<PendingDestinyDiceEffect> _pendingEffects = [];

    private const int MaximumRandomTeleportAttempts = 60;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<DestinyDiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DestinyDiceComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<DestinyDiceComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<DestinyDiceComponent, DiceRolledEvent>(OnRolled);
    }

    private void OnUseInHand(Entity<DestinyDiceComponent> entity, ref UseInHandEvent args)
    {
        entity.Comp.RollerEntity = GetNetEntity(args.User);
        entity.Comp.RolledGrid = GetNetEntity(Transform(args.User).GridUid);
    }
    
    private void OnThrown(Entity<DestinyDiceComponent> entity, ref ThrownEvent args) =>
        entity.Comp.RollerEntity = GetNetEntity(args.User);

    private void OnLand(Entity<DestinyDiceComponent> entity, ref LandEvent args) =>
        entity.Comp.RolledGrid = GetNetEntity(Transform(entity).GridUid);

    private void OnRolled(Entity<DestinyDiceComponent> entity, ref DiceRolledEvent args)
    {
        if (entity.Comp.RollerEntity is null) return;
        if (EffectResults.ContainsKey(GetNetEntity(entity.Owner)))
        {
            _popup.PopupCoordinates(entity.Comp.BusyMessage, Transform(entity).Coordinates);
            return;
        }
        if (entity.Comp.Active)
        {
            ShowCooldownPopup(entity.Owner, entity.Comp);
            return;
        } 
        entity.Comp.NextTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        entity.Comp.Active = true;
        entity.Comp.LastValue = args.Value;
        entity.Comp.LastRoller = entity.Comp.RollerEntity;
    }

    protected override void OnRestart(RoundRestartCleanupEvent ev)
    {
        base.OnRestart(ev);
        _pendingEffects.Clear();
        _pendingEffectGroups.Clear();
    }

    private void RollEffectGroup(EntityUid uid, DestinyDiceComponent dd, int value)
    {
        Dictionary<DestinyDiceEffectGroup, float> targetGroups = [];
        foreach (var group in dd.EffectGroups)
            foreach (var condition in group.RollConditions)
                switch (condition)
                {
                    case SideCondition sideCondition:
                        if(sideCondition.Value == value)
                            targetGroups.Add(group, group.Weight ?? 1);
                        break;
                    case SideRangeCondition rangeCondition:
                        if(value <= rangeCondition.Max && value >= rangeCondition.Min)
                            targetGroups.Add(group, group.Weight ?? 1);
                        break;
                }

        if (targetGroups.Count == 0)
        {
            _popup.PopupCoordinates(dd.NoEffectMessage, Transform(uid).Coordinates);
            return;
        }

        var rolledGroup = _random.Pick(targetGroups);
        if (dd.LastRoller is null) return; // really shouldn't ever happen but meh nice to be safe
        _pendingEffectGroups.Add(new PendingDestinyDiceEffectGroup(uid, GetEntity(dd.LastRoller.Value), GetEntity(dd.RolledGrid), _timing.CurTime, dd, rolledGroup));
    }

    private void ShowCooldownPopup(EntityUid uid, DestinyDiceComponent dd) =>
        _popup.PopupCoordinates(dd.CooldownMessage, Transform(uid).Coordinates);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiceComponent, DestinyDiceComponent>();
        while (query.MoveNext(out var uid, out var dice, out var dd))
        {
            if (_timing.CurTime < dd.NextTriggerTime || !dd.Active)
                continue;
            dd.Active = false;

            if (_timing.CurTime < dd.NextAllowedRollTime)
            {
                ShowCooldownPopup(uid, dd);
                return;
            }

            dd.NextAllowedRollTime = _timing.CurTime + TimeSpan.FromSeconds(dd.RollDelay);
            
            RollEffectGroup(uid, dd, dd.LastValue);
        }

        foreach (var entry in _pendingEffectGroups.ToList())
        {
            if (_timing.CurTime < entry.TriggeredAt + TimeSpan.FromSeconds(entry.Group.Delay))
                continue;
            if (!TryComp<DestinyDiceComponent>(entry.Uid, out var comp)) continue;
            foreach (var condition in entry.Group.TriggerConditions)
            {
                if (TargetedExecution((entry.Uid, comp), condition, entry.Roller, (targets) =>
                    {
                        var passed = condition.FlipCondition;
                        if (targets.Any(target => CheckTriggerCondition(condition, target, (entry.Uid, comp), entry.Roller, entry.Grid)))
                            passed = !condition.FlipCondition;

                        return passed;
                    })) continue;
                if (entry.Group.FailureMessage != null)
                    _popup.PopupCoordinates(entry.Group.FailureMessage, Transform(entry.Uid).Coordinates);
                break;
            }
            foreach (var effect in entry.Group.Effects)
            {
                _pendingEffects.Add(new PendingDestinyDiceEffect(entry.Uid, entry.Roller, entry.Grid, _timing.CurTime, entry.Component, effect, entry.Group, entry.Group.Delay));
            }
            if(entry.Group.SuccessMessage is not null) _popup.PopupCoordinates(entry.Group.SuccessMessage, Transform(entry.Uid).Coordinates);
            _pendingEffectGroups.Remove(entry);
        }
        
        foreach (var entry in _pendingEffects.ToList())
        {
            if (_timing.CurTime < entry.TriggeredAt + TimeSpan.FromSeconds(entry.GroupDelay) + TimeSpan.FromSeconds(entry.Effect.Delay))
                continue;
            // this way effects can do stuff both client and server side :3
            if (entry.Effect.TargetClient)
                RaiseNetworkEvent(new DestinyDiceEffectExecutionEvent(GetNetEntity(entry.Uid),
                    GetNetEntity(entry.Roller), GetNetEntity(entry.Grid), entry.Effect, entry.Group));
            else
                RaiseLocalEvent(new DestinyDiceEffectExecutionEvent(GetNetEntity(entry.Uid), GetNetEntity(entry.Roller),
                    GetNetEntity(entry.Grid), entry.Effect, entry.Group));
            _pendingEffects.Remove(entry);
            if (!EffectResults.TryGetValue(GetNetEntity(entry.Uid), out var value)) continue;
            if (!entry.Group.Effects.All(eff => value.ContainsKey(eff))) continue;
            RaiseNetworkEvent(new DestinyDiceEffectGroupFinishEvent(GetNetEntity(entry.Uid), entry.Group));
            RaiseLocalEvent(new DestinyDiceEffectGroupFinishEvent(GetNetEntity(entry.Uid), entry.Group));
        }
    }
    
    protected override bool ExecuteEffect(IDestinyDiceEffect effect, DestinyDiceEffectGroup group, EntityUid target, Entity<DestinyDiceComponent> entity, EntityUid roller, EntityUid? grid)
    {
        switch (effect)
        {
            case EmptyEffect:
                return true;
            case AddComponentEffect addComponentEffect:
                return true;
            case AddGameRuleEffect addGameRuleEffect:
                {
                    if (!_proto.TryIndex(addGameRuleEffect.Proto, out var proto)) return false;
                    if (!proto.Parents!.Contains("BaseGameRule")) return false;
                    _ticker.AddGameRule(proto.ID);
                    return true;
                }
            case ArmStationNukeEffect armStationNukeEffect:
                {
                    var station = _station.GetOwningStation(grid);
                    if (station is null) return false;
                    var data = Comp<StationDataComponent>(station.Value);
                    EntityUid? nukeEntity = null;
                    var nukeQuery = EntityQueryEnumerator<NukeComponent>();
                    while (nukeQuery.MoveNext(out var uid, out var nuke))
                    {
                        if(data.Grids.All(target => target != grid)) break;
                        if (!Transform(uid).Anchored) continue; // can't arm if unanchored
                        nukeEntity = uid;
                        break;
                    }
                    if (nukeEntity is null) return false;
                
                    var diskQuery = EntityQueryEnumerator<NukeDiskComponent>();
                    while (diskQuery.MoveNext(out var uid, out var disk))
                    {
                        if (GetEntity(disk.OwningStation) != station) continue;
                        _container.Insert(uid, _container.GetContainer(nukeEntity.Value, "Nuke"));
                        _nuke.ArmBomb(nukeEntity.Value);
                        break;
                    }
                    return true;
                }
            case CargoPurchaseEffect cargoPurchaseEffect:
                {
                    if (cargoPurchaseEffect.Product is null) return false;
                    if (!_proto.TryIndex(new ProtoId<CargoProductPrototype>(cargoPurchaseEffect.Product),
                            out var proto)) return false;
                    var station = _station.GetOwningStation(grid);
                    if (station is null) return false;
                    if (!TryComp<StationBankAccountComponent>(station.Value, out var component)) return false;
                    var account = cargoPurchaseEffect.Account ?? component.PrimaryAccount;
                    if (!cargoPurchaseEffect.IsFree)
                        _cargo.UpdateBankAccount((station.Value, component), -proto.Cost * cargoPurchaseEffect.Quantity, account);
                    _cargo.AddAndApproveOrder(station.Value, proto.Product, proto.Name, proto.Cost,
                        cargoPurchaseEffect.Quantity, MetaData(entity).EntityName, proto.Description,
                        MetaData(entity).EntityName, Comp<StationCargoOrderDatabaseComponent>(station.Value),
                        account, (station.Value, Comp<StationDataComponent>(station.Value)));
                    return true;
                }
            case ChangeScaleEffect changeScaleEffect:
                {
                    _scale.SetSpriteScale(target, changeScaleEffect.Scale);
                    return true;
                }
            case ConvertToAntagonistEffect convertToAntagonistEffect:
                return true;
            case DeletePrototypeEffect deletePrototypeEffect:
                {
                    if (effect.TargetPlayer) return false;
                    List<EntityUid> entities;
                    if (!_proto.TryIndex(effect.TargetProto, out var proto)) return false;
                    if (effect.Range < 0)
                        entities = GetAllPrototypes(effect, proto);
                    else if (float.IsPositiveInfinity(effect.Range))
                        entities = GetPrototypesOnMap(effect, proto, Transform(entity).MapID);
                    else
                        entities = GetPrototypesNearby(effect, proto, entity, effect.Range);
                    if (entities.Count == 0) return false;
                    foreach (var _ in entities) QueueDel(target);
                    return true;
                }
            case ExplosionEffect explosionEffect:
                {
                    var mapCoords = new MapCoordinates(_transform.GetMapCoordinates(target).Position,
                        GetCorrectTransform(target, entity, roller).MapID);
                    if (!_proto.HasIndex<ExplosionPrototype>(explosionEffect.TypeId)) return false;
                    _explosion.QueueExplosion(mapCoords, explosionEffect.TypeId, explosionEffect.TotalIntensity,
                        explosionEffect.Slope, explosionEffect.MaxIntensity, entity, explosionEffect.TileBreakScale,
                        explosionEffect.MaxTileBreak, explosionEffect.CanCreateVacuum);
                    return true;
                }
            case InjectReagentEffect injectReagentEffect:
                {
                    if (!_proto.HasIndex<ReagentPrototype>(injectReagentEffect.ReagentProto)) return false;
                    if (!_solution.TryGetSolution(target, injectReagentEffect.SolutionName,
                            out var solution)) return false;
                    return _solution.TryAddReagent(solution.Value, injectReagentEffect.ReagentProto,
                        injectReagentEffect.Quantity);
                }
            case KillTargetEffect killTargetEffect:
                {
                    KillEntity(target);
                    return true;
                }
            case ModifyComponentEffect modifyComponentEffect:
                return true;
            case RandomTeleportationEffect randomTeleportationEffect:
                {
                    var valid = false;
                    // lifted from SharedPortalSystem
                    var xform = GetCorrectTransform(target, entity, roller);
                    if (xform.MapUid is null) return false;
                    var coords = xform.Coordinates;
                    var newCoords =
                        coords.Offset(_random.NextVector2(randomTeleportationEffect.TeleportationRange));
                    for (var i = 0; i < MaximumRandomTeleportAttempts; i++)
                    {
                        var randVector = _random.NextVector2(randomTeleportationEffect.TeleportationRange);
                        newCoords = coords.Offset(randVector);

                        var mapCoords = _transform.ToMapCoordinates(newCoords);

                        if (_lookup.AnyEntitiesIntersecting(mapCoords, LookupFlags.Static))
                            continue;

                        var hasGrid = _mapManager.TryFindGridAt(xform.MapUid!.Value, mapCoords.Position,
                            out var targetGridUid, out var targetGrid);

                        if (!hasGrid && !randomTeleportationEffect.AllowSpace)
                            continue;

                        if (randomTeleportationEffect.StayOnCurrentGrid)
                        {
                            if (!hasGrid)
                                continue;

                            if (targetGridUid != xform.GridUid)
                                continue;
                        }

                        if (randomTeleportationEffect.StayOnStation)
                        {
                            if (TryComp<StationMemberComponent>(xform.GridUid, out var currentStationMember))
                            {
                                if (!hasGrid || !TryComp<StationMemberComponent>(targetGridUid,
                                        out var targetStationMember))
                                    continue;
                                if (targetStationMember.Station != currentStationMember.Station)
                                    continue;
                            }
                        }

                        valid = true;
                        break;
                    }

                    if (!valid) return false;
                    _transform.SetCoordinates(target, newCoords);
                    return true;
                }
            case RemoveComponentEffect removeComponentEffect:
                break;
            case SendToChessDimensionEffect sendToChessDimensionEffect:
                {
                    ChessSmite(target);
                    return true;
                }
            case SpawnGasMixtureEffect spawnGasMixtureEffect:
                {
                    var transform = GetCorrectTransform(target, entity, roller);
                    var pos = _transform.GetGridOrMapTilePosition(target, transform);
                    GasMixture? environment = null;
                
                    if (_atmos.IsTileSpace(transform.GridUid, transform.MapUid, pos)) return false;
                    environment = _atmos.GetContainingMixture((target, transform), true, true);
                    if (environment is null) return false;

                    var merge = new GasMixture(spawnGasMixtureEffect.Volume) { Temperature = spawnGasMixtureEffect.Temperature };
                    merge.SetMoles(spawnGasMixtureEffect.Gas, spawnGasMixtureEffect.Moles);
                    _atmos.Merge(environment, merge);
                    return true;
                }
            case SpawnPrototypeEffect spawnPrototypeEffect:
                {
                    // List<EntityCoordinates> coordinates = [];
                        
                    // coordinates.AddRange(entities.Select(target =>
                        // GetCorrectTransform(target, entity, roller).Coordinates));

                    var passed = false;
                    foreach (var proto in spawnPrototypeEffect.Protos)
                    // foreach (var coordinate in coordinates)
                    {
                        if (!_proto.Resolve(proto.Id, out var validPrototype)) continue;
                        Spawn(validPrototype.ID, GetCorrectTransform(target, entity, roller).Coordinates);
                        passed = true;
                    }

                    return passed;
                }
            case StationAnnouncementEffect stationAnnouncementEffect:
                {
                    var color = Color.FromHex(stationAnnouncementEffect.Color);
                    if (stationAnnouncementEffect.Global)
                        _chat.DispatchGlobalAnnouncement(Loc.GetString(stationAnnouncementEffect.Message),
                            Loc.GetString(stationAnnouncementEffect.Sender), true, stationAnnouncementEffect.Sound,
                            color);
                    else
                        _chat.DispatchStationAnnouncement(entity, Loc.GetString(stationAnnouncementEffect.Message),
                            Loc.GetString(stationAnnouncementEffect.Sender), true, stationAnnouncementEffect.Sound,
                            color);
                    return true;
                }
            case SwapTeleportationEffect swapTeleportationEffect:
                {
                    var coords = GetCorrectTransform(target, entity, roller);
                    List<EntityUid>? destinationPool = null;
                    switch (swapTeleportationEffect)
                    {
                        case { SecondTargetPlayers: true } when effect.Range < 0:
                            destinationPool = GetAllPlayers(effect);
                            break;
                        case { SecondTargetPlayers: true } when float.IsPositiveInfinity(effect.Range):
                            destinationPool = GetPlayersOnMap(effect, Transform(entity).MapID);
                            break;
                        case { SecondTargetPlayers: true }:
                            destinationPool = GetPlayersNearby(effect, entity, effect.Range);
                            break;
                        case { SecondTargetEntity: true }:
                            {
                                if (!_proto.TryIndex(swapTeleportationEffect.SecondTargetProto, out var proto))
                                    return false;
                                if (effect.Range < 0)
                                    destinationPool = GetAllPrototypes(effect, proto);
                                else if (float.IsPositiveInfinity(effect.Range))
                                    destinationPool = GetPrototypesOnMap(effect, proto, Transform(entity).MapID);
                                else
                                    destinationPool = GetPrototypesNearby(effect, proto, entity, effect.Range);
                                break;
                            }
                    }

                    if (destinationPool is null) return false;

                    EntityUid? destination = null;
                    while (destination is null || destination == target)
                        destination = _random.PickAndTake(destinationPool); // just in case it picks the original target
                    var destinationCoords = Transform(destination.Value).Coordinates;

                    _transform.SetCoordinates(target, destinationCoords);
                    _transform.SetCoordinates(destination.Value, coords.Coordinates);
                    return true;
                }
        }

        return false;
    }
    
    #region Smite Redefinitions
    
    private void DustSmite(EntityUid entity)
    {
        QueueDel(entity);
        Spawn("Ash", Transform(entity).Coordinates);
        _popup.PopupEntity(Loc.GetString("admin-smite-turned-ash-other", ("name", entity)), entity, PopupType.LargeCaution);
    }

    private void ChessSmite(EntityUid entity)
    {
        _godmode.EnableGodmode(entity); // So they don't suffocate.
        EnsureComp<TabletopDraggableComponent>(entity);
        RemComp<PhysicsComponent>(entity); // So they can be dragged around.
        var xform = Transform(entity);
        _popup.PopupEntity(Loc.GetString("admin-smite-chess-self"), entity,
            entity, PopupType.LargeCaution);
        _popup.PopupCoordinates(
            Loc.GetString("admin-smite-chess-others", ("name", entity)), xform.Coordinates,
            Filter.PvsExcept(entity), true, PopupType.MediumCaution);
        var board = Spawn("ChessBoard", xform.Coordinates);
        var session = _tabletop.EnsureSession(Comp<TabletopGameComponent>(board));
        _transform.SetMapCoordinates(entity, session.Position);
        _transform.SetWorldRotationNoLerp((entity, xform), Angle.Zero);
    }
    
    #endregion
    
    private void KillEntity(EntityUid entity)
    {
        if (TryComp<DamageableComponent>(entity, out var damageable))
        {
            if (!TryComp<MobThresholdsComponent>(entity, out var thresholds)) DustSmite(entity);
            else
            {
                var state = thresholds.Thresholds.FirstOrDefault(x => x.Value == MobState.Dead);
                // check if state actually grabbed a kvp with a dead state
                if (state.Value != MobState.Dead) DustSmite(entity);
                else
                    _damage.SetDamage(entity, damageable,
                        new DamageSpecifier(_proto.Index(_bluntDamageType), state.Key));
            }
        }
        else DustSmite(entity);
    }
    
    private sealed class PendingDestinyDiceEffectGroup(EntityUid uid, EntityUid roller, EntityUid? grid, TimeSpan triggeredAt, DestinyDiceComponent comp, DestinyDiceEffectGroup group)
    
    {
        public readonly EntityUid Uid = uid;
        public readonly EntityUid Roller = roller;
        public readonly EntityUid? Grid = grid;
        public readonly TimeSpan TriggeredAt = triggeredAt;
        public readonly DestinyDiceComponent Component = comp;
        public readonly DestinyDiceEffectGroup Group = group;
    }
    
    private sealed class PendingDestinyDiceEffect(EntityUid uid, EntityUid roller, EntityUid? grid, TimeSpan triggeredAt, DestinyDiceComponent comp, IDestinyDiceEffect effect, DestinyDiceEffectGroup group, float groupDelay)
    {
        public readonly EntityUid Uid = uid;
        public readonly EntityUid Roller = roller;
        public readonly EntityUid? Grid = grid;
        public readonly TimeSpan TriggeredAt = triggeredAt;
        public readonly DestinyDiceComponent Component = comp;
        public readonly IDestinyDiceEffect Effect = effect;
        public readonly DestinyDiceEffectGroup Group = group;
        public readonly float GroupDelay = groupDelay;
    }

    
}