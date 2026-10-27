using System.Linq;
using Content.Server._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Alert;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    [Dependency] private SharedHandsSystem _controlHands = default!;
    [Dependency] private RemoteControlCommandSystem _controlCommands = default!;
    [Dependency] private SharedActionsSystem _controlActions = default!;
    [Dependency] private BodyControlHandContextSystem _handContext = default!;
    [Dependency] private PvsOverrideSystem _controlPvs = default!;
    [Dependency] private IGameTiming _controlTiming = default!;

    private sealed class PresentationView(ICommonSession session)
    {
        public readonly ICommonSession Session = session;
        public HashSet<EntityUid> Entities = new();
        public TimeSpan NextUpdate;
    }
    private readonly Dictionary<EntityUid, PresentationView> _controlViews = new();

    private void InitializeInteractions()
    {
        var binds = CommandBinds.Builder;
        foreach (var (key, command) in BodyControlBindings.All.Where(binding => binding.Key != EngineKeyFunctions.UseSecondary))
            binds.BindBefore(key, new PointerInputCmdHandler((session, coordinates, target) =>
            {
                if (session?.AttachedEntity is not { } actor
                    || !TryComp<BodyControlSourceComponent>(actor, out var source) || source.ExternallyDriven)
                    return false;
                ExecuteControlCommand(actor, new(command, target: target.IsValid() ? GetNetEntity(target) : null,
                    coordinates: GetNetCoordinates(coordinates)));
                return true;
            }), typeof(SharedHandsSystem), typeof(SharedInteractionSystem));
        binds.Register<BodyControlSystem>();
    }

    private void ShutdownInteractions()
    {
        foreach (var actor in _controlViews.Keys.ToArray())
            RemoveControlPresentation(actor);
        CommandBinds.Unregister<BodyControlSystem>();
    }

    [SubscribeNetworkEvent]
    private void OnControlCommand(BodyControlCommandEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is { } actor)
            ExecuteControlCommand(actor, args);
    }

    [SubscribeLocalEvent]
    private void OnControlHandRequest(Entity<BodyControlSourceComponent> ent, ref BodyControlHandRequestEvent args)
        => ExecuteControlCommand(ent, new(args.Command, args.Hand));

    private void InitializeControlHand(EntityUid actor, BodyControlSourceComponent source)
    {
        if (source.ExternallyDriven || !TryComp<HandsComponent>(source.Body, out var hands))
            return;
        var selected = source.SelectedHand != null && _handContext.AllowsHand(source.Body, source.SelectedHand, source.Access)
            ? source.SelectedHand
            : hands.ActiveHandId is { } active && _handContext.AllowsHand(source.Body, active, source.Access)
                ? active
                : hands.SortedHands.FirstOrDefault(hand => _handContext.AllowsHand(source.Body, hand, source.Access));
        if (selected == source.SelectedHand)
            return;
        source.SelectedHand = selected;
        Dirty(actor, source);
    }

    private void ExecuteControlCommand(EntityUid actor, BodyControlCommandEvent command)
    {
        if (!TryGetControl(actor, out var body, out var access)
            || !TryComp<BodyControlSourceComponent>(actor, out var source) || source.ExternallyDriven)
            return;
        InitializeControlHand(actor, source);
        var hand = command.Hand ?? source.SelectedHand;
        var handAllowed = hand != null && _handContext.AllowsHand(body, hand, access);
        if (command.Command is BodyControlCommand.SelectHand or BodyControlCommand.CycleHand or BodyControlCommand.CycleHandReverse)
        {
            if (!access.Allows("body.hands") || !TryComp<HandsComponent>(body, out var hands))
            {
                Deny(actor, "hands");
                return;
            }
            var allowed = hands.SortedHands.Where(id => _handContext.AllowsHand(body, id, access)).ToList();
            if (command.Command != BodyControlCommand.SelectHand && allowed.Count > 0)
            {
                var index = allowed.IndexOf(source.SelectedHand ?? "");
                hand = allowed[(index + (command.Command == BodyControlCommand.CycleHandReverse ? allowed.Count - 1 : 1)) % allowed.Count];
            }
            if (hand == null || !allowed.Contains(hand))
            {
                Deny(actor, "hands");
                return;
            }
            source.SelectedHand = hand;
            Dirty(actor, source);
            return;
        }

        var handsCommand = command.Command is BodyControlCommand.UseHand or BodyControlCommand.AltUseHand
            or BodyControlCommand.ActivateHand or BodyControlCommand.Drop;
        if (handsCommand && (!access.Allows("body.hands") || !handAllowed))
        {
            Deny(actor, "hands");
            return;
        }
        if (command.Command == BodyControlCommand.HandClick)
        {
            if (!handAllowed || hand == null)
            {
                Deny(actor, "hands");
                return;
            }
            if (hand != source.SelectedHand && _controlHands.GetHeldItem(body, hand) == null)
            {
                source.SelectedHand = hand;
                Dirty(actor, source);
                return;
            }
            var selected = source.SelectedHand;
            if (selected == null || !_handContext.AllowsHand(body, selected, access))
            {
                Deny(actor, "hands");
                return;
            }
            var operation = hand == selected ? RemoteControlHandAction.Use
                : _controlHands.GetHeldItem(body, selected) == null ? RemoteControlHandAction.MoveToActive
                : RemoteControlHandAction.InteractWithHand;
            _handContext.Execute(body, actor, access, selected,
                () => _controlCommands.ExecuteHand(body, hand, operation), "body.hands");
            return;
        }
        if (handsCommand && source.SelectedHand != hand)
        {
            source.SelectedHand = hand;
            Dirty(actor, source);
        }
        if (command.Command == BodyControlCommand.Action)
        {
            if (command.Action is not { } netAction || !TryGetEntity(netAction, out var action)
                || !CanControlAction(body, action.Value, access))
            {
                Deny(actor, "actions");
                return;
            }
            _handContext.Execute(body, actor, access, handAllowed ? hand : null, () =>
                _controlCommands.ExecuteAction(body, action.Value, ResolveTarget(command.Target), command.Coordinates),
                _controlActions.GetEvent(action.Value) is ToggleCombatActionEvent ? "body.interact" : "body.actions");
            return;
        }
        if (command.Command is BodyControlCommand.InventoryUse or BodyControlCommand.InventoryStorage
            or BodyControlCommand.InventoryActivate or BodyControlCommand.InventoryAltActivate)
        {
            if (!access.Allows("body.inventory"))
            {
                Deny(actor, "inventory");
                return;
            }
            if (command.Command == BodyControlCommand.InventoryUse && HasComp<HandsComponent>(body) && !handAllowed)
            {
                Deny(actor, "hands");
                return;
            }
            if (command.Slot is not { } slot)
                return;
            _handContext.Execute(body, actor, access, handAllowed ? hand : null, () =>
            {
                if (command.Command is BodyControlCommand.InventoryUse or BodyControlCommand.InventoryStorage)
                    _controlCommands.ExecuteInventory(body, slot,
                        command.Command == BodyControlCommand.InventoryUse ? RemoteControlInventoryAction.UseSlot : RemoteControlInventoryAction.OpenStorage);
                else if (EntityManager.System<InventorySystem>().TryGetSlotEntity(body, slot, out var equipped))
                    _controlCommands.ExecuteInteraction(body, Transform(body).Coordinates, target: equipped,
                        activateInWorld: command.Command == BodyControlCommand.InventoryActivate,
                        altInteract: command.Command == BodyControlCommand.InventoryAltActivate, hand: hand);
            }, "body.inventory");
            return;
        }
        if (command.Command == BodyControlCommand.Alert)
        {
            if (!access.Allows("body.interact"))
            {
                Deny(actor, "interact");
                return;
            }
            var alerts = EntityManager.System<AlertsSystem>();
            if (command.Slot is { } alert && alerts.TryGet(alert, out var prototype) && alerts.IsShowingAlert(body, prototype.ID))
                _handContext.Execute(body, actor, access, handAllowed ? hand : null, () => alerts.ActivateAlert(body, prototype), "body.interact");
            return;
        }
        if (!handsCommand && !access.Allows("body.interact"))
        {
            Deny(actor, "interact");
            return;
        }
        // A scoped world interaction must identify the permitted hand. Handless full-body mobs may still act.
        if (!handsCommand && HasComp<HandsComponent>(body) && !handAllowed
            && command.Command != BodyControlCommand.ToggleCombat)
        {
            Deny(actor, "hands");
            return;
        }

        var target = ResolveTarget(command.Target);
        var coordinates = command.Coordinates is { } netCoords ? GetCoordinates(netCoords) : Transform(body).Coordinates;
        _handContext.Execute(body, actor, access, handAllowed ? hand : null, () =>
        {
            switch (command.Command)
            {
                case BodyControlCommand.UseHand:
                case BodyControlCommand.AltUseHand:
                case BodyControlCommand.ActivateHand:
                    if (_controlHands.TryGetHeldItem(body, hand, out var held))
                    {
                        if (HasComp<ActivatableUIComponent>(held) && !access.Allows("body.inventory"))
                        {
                            Deny(actor, "inventory");
                            break;
                        }
                        _controlCommands.ExecuteHand(body, hand!,
                            command.Command == BodyControlCommand.ActivateHand ? RemoteControlHandAction.Activate
                                : command.Command == BodyControlCommand.AltUseHand ? RemoteControlHandAction.AltUse
                                : RemoteControlHandAction.Use);
                    }
                    break;
                case BodyControlCommand.Drop:
                    _controlCommands.ExecuteInteraction(body, coordinates, RemoteControlInteractionAction.Drop, hand: hand);
                    break;
                case BodyControlCommand.ToggleCombat:
                    if (TryComp<CombatModeComponent>(body, out var combat))
                        EntityManager.System<SharedCombatModeSystem>().SetInCombatMode(body, !combat.IsInCombatMode, combat);
                    break;
                case BodyControlCommand.Interact:
                case BodyControlCommand.AltInteract:
                case BodyControlCommand.ActivateWorld:
                case BodyControlCommand.Throw:
                case BodyControlCommand.Pull:
                case BodyControlCommand.Shoot:
                case BodyControlCommand.MovePulledObject:
                    var alt = command.Command == BodyControlCommand.AltInteract
                        || command.Command == BodyControlCommand.Shoot
                        && EntityManager.System<SharedGunSystem>().TryGetGun(body, out var shootingGun) && !shootingGun.Comp.UseKey;
                    var shoot = command.Command != BodyControlCommand.ActivateWorld
                        && EntityManager.System<SharedCombatModeSystem>().IsInCombatMode(body)
                        && _controlHands.GetHeldItem(body, hand) is { } gunEntity
                        && TryComp<GunComponent>(gunEntity, out var gun) && gun.UseKey != alt;
                    var interaction = command.Command switch
                    {
                        BodyControlCommand.Throw => RemoteControlInteractionAction.Throw,
                        BodyControlCommand.Pull => RemoteControlInteractionAction.TryPull,
                        BodyControlCommand.Shoot => RemoteControlInteractionAction.Shoot,
                        BodyControlCommand.MovePulledObject => RemoteControlInteractionAction.MovePulledObject,
                        _ => shoot ? RemoteControlInteractionAction.Shoot : RemoteControlInteractionAction.Interact,
                    };
                    _controlCommands.ExecuteInteraction(body, coordinates, interaction, target, alt,
                        activateInWorld: command.Command == BodyControlCommand.ActivateWorld, hand: hand);
                    break;
            }
        }, handsCommand ? "body.hands" : "body.interact");
    }

    private EntityUid? ResolveTarget(NetEntity? target)
        => target is { } entity && TryGetEntity(entity, out var uid) && !TerminatingOrDeleted(uid.Value) ? uid : null;

    private bool CanControlAction(EntityUid body, EntityUid action, BodyControlAccess access)
    {
        return !TerminatingOrDeleted(action)
               && (_controlActions.GetEvent(action) is ToggleCombatActionEvent ? access.Allows("body.interact") : access.Allows("body.actions"))
               && _controlCommands.GetActionEntities(body).Contains(action)
               && _controlActions.GetEvent(action) is not IBodyControlPersonalAction
               && (_controlActions.GetEvent(action) is ToggleCombatActionEvent || access.FullControl || access.Actions == null
                   || MetaData(action).EntityPrototype is { } prototype && access.Actions.Contains(prototype.ID));
    }

    [SubscribeLocalEvent]
    private void OnControlledMelee(Entity<BodyControlTargetComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!_handContext.IsExecuting(ent) && !CanNativeInteract(ent) && !HasExternalController(ent))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnControlledShot(Entity<BodyControlTargetComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!_handContext.IsExecuting(ent) && !CanNativeInteract(ent) && !HasExternalController(ent))
            args.Cancel();
    }

    private bool CanNativeInteract(EntityUid body)
        => TryGetControl(body, out var controlled, out var access) && controlled == body && access.Allows("body.interact");

    private bool HasExternalController(Entity<BodyControlTargetComponent> body)
        => body.Comp.Controllers.Any(controller => TryComp<BodyControlSourceComponent>(controller, out var source)
            && source.ExternallyDriven && TryGetControl(controller, out var controlled, out _) && controlled == body.Owner);

    [SubscribeLocalEvent]
    private void OnNativeActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (TryComp<BodyControlSourceComponent>(args.User, out var source) && source.Body == args.User
            && !source.ExternallyDriven
            && !_handContext.IsExecuting(args.User) && !CanControlAction(args.User, ent, source.Access)
            && _controlActions.GetEvent(ent) is not IBodyControlPersonalAction)
        {
            args.Cancelled = true;
            Deny(args.User, "actions");
        }
    }

    private void UpdateControlPresentation(EntityUid actor, BodyControlSourceComponent source)
    {
        if (source.ExternallyDriven || !TryGetControl(actor, out var body, out var access)
            || !TryComp<ActorComponent>(actor, out var player)
            || actor == body && access.FullControl)
        {
            RemoveControlPresentation(actor);
            return;
        }
        if (_controlViews.TryGetValue(actor, out var previous) && previous.Session != player.PlayerSession)
            RemoveControlPresentation(actor);
        if (!_controlViews.TryGetValue(actor, out var view))
            _controlViews[actor] = view = new(player.PlayerSession);
        if (_controlTiming.CurTime < view.NextUpdate)
            return;
        view.NextUpdate = _controlTiming.CurTime + TimeSpan.FromMilliseconds(250);
        var entities = new HashSet<EntityUid>();
        _controlCommands.CollectVisibleEntities(body, entities, includeInventory: true);
        foreach (var removed in view.Entities.Except(entities))
            _controlPvs.RemoveSessionOverride(removed, view.Session);
        foreach (var added in entities.Except(view.Entities))
            _controlPvs.AddSessionOverride(added, view.Session);
        view.Entities = entities;
    }

    private void RemoveControlPresentation(EntityUid actor)
    {
        if (!_controlViews.Remove(actor, out var view))
            return;
        // The consumer owns the body's view; only remove the item/action overrides added here.
        foreach (var entity in view.Entities)
            _controlPvs.RemoveSessionOverride(entity, view.Session);
    }
}
