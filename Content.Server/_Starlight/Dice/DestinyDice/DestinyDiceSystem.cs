using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Nuke;
using Content.Server.Tabletop;
using Content.Server.Tabletop.Components;
using Content.Shared._Starlight.Dice.DestinyDice;
using Content.Shared._Starlight.Dice;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Dice;
using Content.Shared.Explosion;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nuke;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.Tabletop.Components;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Dice.DestinyDice;

public sealed class DestinyDiceSystem : SharedDestinyDiceSystem
{
    [Dependency] private readonly TabletopSystem _tabletop = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly NukeSystem _nuke = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    
    private readonly List<PendingDestinyDiceEffectGroup> _pendingEffectGroups = [];
    private readonly List<PendingDestinyDiceEffect> _pendingEffects = [];

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
        if (entity.Comp.Active) ShowCooldownPopup(entity.Owner, entity.Comp); 
        entity.Comp.NextTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        entity.Comp.Active = true;
        entity.Comp.LastValue = args.Value; // idk if i will need this tbh
    }

    private void RollEffectGroup(EntityUid uid, DestinyDiceComponent dd, int value)
    {
        Dictionary<DestinyDiceEffectGroup, float> targetGroups = [];
        if (dd.EffectGroups.TryGetValue(value, out var groups))
        {
            foreach (var group in groups)
            {
                targetGroups.Add(group, group.Weight ?? 1);
            }
        }

        if (targetGroups.Count == 0)
        {
            _popup.PopupCoordinates(dd.NoEffectMessage, Transform(uid).Coordinates);
            return;    
        }

        var rolledGroup = _random.Pick(targetGroups);
        _pendingEffectGroups.Add(new PendingDestinyDiceEffectGroup(uid, dd, rolledGroup.Key));
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
            
            RollEffectGroup(uid, dd, dice.CurrentValue);
        }

        foreach (var entry in _pendingEffectGroups.ToList())
        {
            if (_timing.CurTime < entry.Component.NextTriggerTime + TimeSpan.FromSeconds(entry.Group.Delay))
                continue;
            foreach (var effect in entry.Group.Effects)
            {
                _pendingEffects.Add(new PendingDestinyDiceEffect(entry.Uid, entry.Component, effect, entry.Group.Delay));
            }
            if(entry.Group.SuccessMessage is not null) _popup.PopupCoordinates(entry.Group.SuccessMessage, Transform(entry.Uid).Coordinates);
            _pendingEffectGroups.Remove(entry);
        }
        
        foreach (var entry in _pendingEffects.ToList())
        {
            if (_timing.CurTime < entry.Component.NextTriggerTime + TimeSpan.FromSeconds(entry.GroupDelay) + TimeSpan.FromSeconds(entry.Effect.Delay))
                continue;
            // this way effects can do stuff both client and server side :3
            RaiseNetworkEvent(new DestinyDiceEffectExecutionEvent(GetNetEntity(entry.Uid), entry.Effect));
            RaiseLocalEvent(new DestinyDiceEffectExecutionEvent(GetNetEntity(entry.Uid), entry.Effect));
            _pendingEffects.Remove(entry);
        }
    }
    
    protected override void ExecuteEffect(IDestinyDiceEffect effect, Entity<DestinyDiceComponent> entity)
    {
        var roller = GetEntity(entity.Comp.RollerEntity);
        if (roller is null) return; // someone needs to have rolled it.
        
        switch (effect)
        {
            case AddComponentEffect addComponentEffect:
                break;
            case AddGameRuleEffect addGameRuleEffect:
                {
                    if (!_proto.TryIndex(addGameRuleEffect.Proto, out var proto)) break;
                    if (!proto.Parents!.Contains("BaseGameRule")) break;
                    _ticker.AddGameRule(proto.ID);
                    break;
                }
            case ArmStationNukeEffect armStationNukeEffect:
                if (entity.Comp.RolledGrid is null) break;
                var station = _station.GetOwningStation(GetEntity(entity.Comp.RolledGrid));
                if (station is null) break;
                var data = Comp<StationDataComponent>(station.Value);
                EntityUid? nukeEntity = null;
                var nukeQuery = EntityQueryEnumerator<NukeComponent>();
                while (nukeQuery.MoveNext(out var uid, out var nuke))
                {
                    if(data.Grids.All(grid => grid != GetEntity(entity.Comp.RolledGrid))) break;
                    if (!Transform(uid).Anchored) continue; // can't arm if unanchored
                    nukeEntity = uid;
                    break;
                }
                if (nukeEntity is null) break;
                
                var diskQuery = EntityQueryEnumerator<NukeDiskComponent>();
                while (diskQuery.MoveNext(out var uid, out var disk))
                {
                    if (GetEntity(disk.OwningStation) != station) continue;
                    _container.Insert(uid, _container.GetContainer(nukeEntity.Value, "Nuke"));
                    _nuke.ArmBomb(nukeEntity.Value);
                    break;
                }
                break;
            case CargoPurchaseEffect cargoPurchaseEffect:
                break;
            case ChangeScaleEffect changeScaleEffect:
                break;
            case DeletePrototypeEffect deletePrototypeEffect:
                break;
            case ExplosionEffect explosionEffect:
                Log.Log(LogLevel.Info, "triggered");
                var target = explosionEffect.TargetPlayer
                    ? new Entity<TransformComponent>(roller.Value, Comp<TransformComponent>(roller.Value))
                    : new Entity<TransformComponent>(entity.Owner, Comp<TransformComponent>(entity.Owner));
                var mapCoords = new MapCoordinates(_transform.GetMapCoordinates(target).Position, target.Comp.MapID);
                // Log.Log(LogLevel.Info, $"{mapCoords.X}, {mapCoords.Y}, {mapCoords.Position.X}, {mapCoords.Position.Y}, {mapCoords.MapId}, {coords.Coordinates.X}, {coords.Coordinates.Y}, {coords.Coordinates.Position.X}, {coords.Coordinates.Position.Y}, {coords.LocalPosition.X}, {coords.LocalPosition.Y}");
                if (!_proto.HasIndex<ExplosionPrototype>(explosionEffect.TypeId)) break;
                Log.Log(LogLevel.Info, "past break");
                _explosion.QueueExplosion(mapCoords, explosionEffect.TypeId, explosionEffect.TotalIntensity,
                    explosionEffect.Slope, explosionEffect.MaxIntensity, entity, explosionEffect.TileBreakScale,
                    explosionEffect.MaxTileBreak, explosionEffect.CanCreateVacuum);
                Log.Log(LogLevel.Info, "boom");
                break;
            case KillRollerEffect killRollerEffect:
                KillEntity(roller.Value);
                break;
            case ModifyComponentEffect modifyComponentEffect:
                break;
            case RandomTeleportationEffect randomTeleportationEffect:
                break;
            case RemoveComponentEffect removeComponentEffect:
                break;
            case SendToChessDimensionEffect sendToChessDimensionEffect:
                ChessSmite(roller.Value);
                break;
            case SpawnGasMixtureEffect spawnGasMixtureEffect:
                break;
            case SpawnPrototypeEffect spawnPrototypeEffect:
                {
                    List<EntityCoordinates> coordinates = [];
                    if (!spawnPrototypeEffect.SpawnOnPlayer)
                    {
                        if (TryComp<HandsComponent>(roller, out var hands))
                        {
                            var foundMatch = false;
                            if (hands.Hands.Keys
                                .Select(hand => _hands.GetHeldItem((roller.Value, hands), hand))
                                .Any(item => item == entity))
                            {
                                coordinates.Add(Transform(roller.Value).Coordinates);
                                foundMatch = true;
                            }

                            if (!foundMatch) coordinates.Add(Transform(entity).Coordinates);
                        }
                        else coordinates.Add(Transform(entity).Coordinates);
                    }
                    else
                        switch (spawnPrototypeEffect)
                        {
                            case { SpawnOnPlayer: true, SpawnOnMultiple: false }:
                                coordinates.Add(Transform(roller.Value).Coordinates);
                                break;
                            case { SpawnOnPlayer: true, SpawnOnMultiple: true, PlayerRange: < 0 }:
                                {
                                    var entities = EntityManager.GetEntities().Where(HasComp<ActorComponent>)
                                        .Where(ent => !HasComp<GhostComponent>(ent));
                                    coordinates.AddRange(entities.Select(foundEntity =>
                                        Transform(foundEntity).Coordinates));
                                    break;
                                }
                            case { SpawnOnPlayer: true, SpawnOnMultiple: true }
                                when float.IsPositiveInfinity(spawnPrototypeEffect.PlayerRange):
                                {
                                    var entities = new HashSet<Entity<ActorComponent>>();
                                    _lookup.GetEntitiesOnMap(_transform.GetMapId(entity.Owner), entities);

                                    entities.RemoveWhere(ent => HasComp<GhostComponent>(ent));

                                    coordinates.AddRange(entities.Select(foundEntity =>
                                        Transform(foundEntity).Coordinates));
                                    break;
                                }
                            case { SpawnOnPlayer: true, SpawnOnMultiple: true }:
                                {
                                    var entities = _lookup.GetEntitiesInRange(entity.Owner,
                                        spawnPrototypeEffect.PlayerRange);
                                    entities.RemoveWhere(HasComp<GhostComponent>);
                                    coordinates.AddRange(from foundEntity in entities
                                        where HasComp<ActorComponent>(foundEntity)
                                        select Transform(foundEntity).Coordinates);
                                    break;
                                }
                        }

                    foreach (var proto in spawnPrototypeEffect.Protos)
                    foreach (var coordinate in coordinates)
                    {
                        if (!_proto.Resolve(proto.Id, out var validPrototype)) continue;
                        Spawn(validPrototype.ID, coordinate);
                    }

                    break;
                }
            case StationAnnouncementEffect stationAnnouncementEffect:
                {
                    var color = Color.FromHex(stationAnnouncementEffect.Color);
                    _chat.DispatchGlobalAnnouncement(Loc.GetString(stationAnnouncementEffect.Message), Loc.GetString(stationAnnouncementEffect.Sender), true, stationAnnouncementEffect.Sound, color);
                    break;
                }
            case SwapTeleportationEffect swapTeleportationEffect:
                break;
        }
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
    
    private sealed class PendingDestinyDiceEffectGroup(EntityUid uid, DestinyDiceComponent comp, DestinyDiceEffectGroup group)
    {
        public readonly EntityUid Uid = uid;
        public readonly DestinyDiceComponent Component = comp;
        public readonly DestinyDiceEffectGroup Group = group;
    }
    
    private sealed class PendingDestinyDiceEffect(EntityUid uid, DestinyDiceComponent comp, IDestinyDiceEffect effect, float groupDelay)
    {
        public readonly EntityUid Uid = uid;
        public readonly DestinyDiceComponent Component = comp;
        public readonly IDestinyDiceEffect Effect = effect;
        public readonly float GroupDelay = groupDelay;
    }
}