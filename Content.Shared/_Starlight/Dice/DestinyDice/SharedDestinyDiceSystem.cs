using System.Linq;
using Content.Shared._Starlight.Dice;
using Content.Shared.Chat;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dice;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Admeme.DestinyDice;

[Virtual]
public class SharedDestinyDiceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly SharedVerbSystem _verb = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    
    private readonly List<PendingDestinyDiceEffectGroup> _pendingEffectGroups = [];
    private readonly List<PendingDestinyDiceEffect> _pendingEffects = [];
    
    private static readonly ProtoId<DamageTypePrototype> _bluntDamageType = "Blunt";
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestinyDiceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DestinyDiceComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<DestinyDiceComponent, DiceRolledEvent>(OnRolled);
    }

    private void OnUseInHand(Entity<DestinyDiceComponent> entity, ref UseInHandEvent args) => entity.Comp.RollerEntity = args.User;
    private void OnThrown(Entity<DestinyDiceComponent> entity, ref ThrownEvent args) => entity.Comp.RollerEntity = args.User!.Value; // Starlight

    private void OnRolled(Entity<DestinyDiceComponent> entity, ref DiceRolledEvent args)
    {
        if (entity.Comp.Active) ShowCooldownPopup(entity.Owner, entity.Comp); 
        entity.Comp.NextTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        entity.Comp.Active = true;
        entity.Comp.LastValue = args.Value; // idk if i will need this tbh
    }

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
            ExecuteEffect(entry.Effect, (entry.Uid, entry.Component));
            _pendingEffects.Remove(entry);
        }
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

    private void ExecuteEffect(IDestinyDiceEffect effect, Entity<DestinyDiceComponent> entity)
    {
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
                break;
            case CargoPurchaseEffect cargoPurchaseEffect:
                break;
            case ChangeScaleEffect changeScaleEffect:
                break;
            case DeletePrototypeEffect deletePrototypeEffect:
                break;
            case ExplosionEffect explosionEffect:
                break;
            case KillRollerEffect killRollerEffect:
                KillEntity(entity.Comp.RollerEntity);
                break;
            case ModifyComponentEffect modifyComponentEffect:
                break;
            case RandomTeleportationEffect randomTeleportationEffect:
                break;
            case RemoveComponentEffect removeComponentEffect:
                break;
            case SendToChessDimensionEffect sendToChessDimensionEffect:
                
                break;
            case SpawnGasMixtureEffect spawnGasMixtureEffect:
                break;
            case SpawnPrototypeEffect spawnPrototypeEffect:
                {
                    List<EntityCoordinates> coordinates = [];
                    if (!spawnPrototypeEffect.SpawnOnPlayer)
                    {
                        if (TryComp<HandsComponent>(entity.Comp.RollerEntity, out var hands))
                        {
                            var foundMatch = false;
                            if (hands.Hands.Keys
                                .Select(hand => _hands.GetHeldItem((entity.Comp.RollerEntity, hands), hand))
                                .Any(item => item == entity))
                            {
                                coordinates.Add(Transform(entity.Comp.RollerEntity).Coordinates);
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
                                coordinates.Add(Transform(entity.Comp.RollerEntity).Coordinates);
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
                        EntityManager.SpawnEntity(validPrototype.ID, coordinate);
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
            case TransmutationEffect transmutationEffect:
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
    
    private void ChessSmite(EntityUid entity){}
    
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