using System.Linq;
using Content.Server._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    [Dependency] private SharedHandsSystem _controlHands = default!;
    [Dependency] private RemoteControlConsoleSystem _remoteControl = default!;
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
        foreach (var (key, command) in ControlBindings())
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

    private static (BoundKeyFunction, BodyControlCommand)[] ControlBindings() =>
    [
        (EngineKeyFunctions.Use, BodyControlCommand.Interact),
        (ContentKeyFunctions.AltActivateItemInWorld, BodyControlCommand.AltInteract),
        (ContentKeyFunctions.ActivateItemInWorld, BodyControlCommand.ActivateWorld),
        (ContentKeyFunctions.UseItemInHand, BodyControlCommand.UseHand),
        (ContentKeyFunctions.AltUseItemInHand, BodyControlCommand.AltUseHand),
        (ContentKeyFunctions.SwapHands, BodyControlCommand.CycleHand),
        (ContentKeyFunctions.SwapHandsReverse, BodyControlCommand.CycleHandReverse),
        (ContentKeyFunctions.Drop, BodyControlCommand.Drop),
        (ContentKeyFunctions.ThrowItemInHand, BodyControlCommand.Throw),
        (ContentKeyFunctions.TryPullObject, BodyControlCommand.Pull),
    ];

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
                return;
            var allowed = hands.SortedHands.Where(id => _handContext.AllowsHand(body, id, access)).ToList();
            if (command.Command != BodyControlCommand.SelectHand && allowed.Count > 0)
            {
                var index = allowed.IndexOf(source.SelectedHand ?? "");
                hand = allowed[(index + (command.Command == BodyControlCommand.CycleHandReverse ? allowed.Count - 1 : 1)) % allowed.Count];
            }
            if (hand == null || !allowed.Contains(hand))
                return;
            source.SelectedHand = hand;
            Dirty(actor, source);
            return;
        }

        var handsCommand = command.Command is BodyControlCommand.UseHand or BodyControlCommand.AltUseHand
            or BodyControlCommand.ActivateHand or BodyControlCommand.Drop;
        if (handsCommand && (!access.Allows("body.hands") || !handAllowed))
            return;
        if (handsCommand && source.SelectedHand != hand)
        {
            source.SelectedHand = hand;
            Dirty(actor, source);
        }
        if (command.Command == BodyControlCommand.Action)
        {
            if (command.Action is not { } netAction || !TryGetEntity(netAction, out var action)
                || !CanControlAction(body, action.Value, access))
                return;
            _handContext.Execute(body, actor, access, handAllowed ? hand : null, () =>
                _remoteControl.ExecuteRemoteAction(body, action.Value, ResolveTarget(command.Target), command.Coordinates));
            return;
        }
        if (!handsCommand && !access.Allows("body.interact"))
            return;
        if (!access.FullControl && command.Command is BodyControlCommand.Throw or BodyControlCommand.Pull
            or BodyControlCommand.Shoot or BodyControlCommand.MovePulledObject)
            return;
        // A scoped world interaction must identify the permitted hand. Handless full-body mobs may still act.
        if (!handsCommand && HasComp<HandsComponent>(body) && !handAllowed
            && command.Command != BodyControlCommand.ToggleCombat)
            return;

        var target = ResolveTarget(command.Target);
        var coordinates = command.Coordinates is { } netCoords ? GetCoordinates(netCoords) : Transform(body).Coordinates;
        _handContext.Execute(body, actor, access, handAllowed ? hand : null, () =>
        {
            switch (command.Command)
            {
                case BodyControlCommand.UseHand:
                case BodyControlCommand.AltUseHand:
                case BodyControlCommand.ActivateHand:
                    if (_controlHands.TryGetHeldItem(body, hand, out var held)
                        && (access.FullControl || !HasComp<ActivatableUIComponent>(held)))
                    {
                        _remoteControl.ExecuteRemoteHand(body, new RemoteControlHandMessage
                        {
                            Hand = hand!,
                            Action = command.Command == BodyControlCommand.ActivateHand ? RemoteControlHandAction.Activate
                                : command.Command == BodyControlCommand.AltUseHand ? RemoteControlHandAction.AltUse
                                : RemoteControlHandAction.Use,
                        });
                    }
                    break;
                case BodyControlCommand.Drop:
                    _remoteControl.ExecuteRemoteInteraction(body, new RemoteControlInteractionMessage
                    {
                        Coordinates = GetNetCoordinates(coordinates),
                        Action = RemoteControlInteractionAction.Drop,
                    }, hand);
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
                    if (!access.FullControl && target is { } uiTarget && HasComp<ActivatableUIComponent>(uiTarget))
                        break;
                    var alt = command.Command == BodyControlCommand.AltInteract
                        || command.Command == BodyControlCommand.Shoot
                        && EntityManager.System<SharedGunSystem>().TryGetGun(body, out var shootingGun) && !shootingGun.Comp.UseKey;
                    var shoot = command.Command != BodyControlCommand.ActivateWorld
                        && EntityManager.System<SharedCombatModeSystem>().IsInCombatMode(body)
                        && _controlHands.GetHeldItem(body, hand) is { } gunEntity
                        && TryComp<GunComponent>(gunEntity, out var gun) && gun.UseKey != alt;
                    _remoteControl.ExecuteRemoteInteraction(body, new RemoteControlInteractionMessage
                    {
                        Coordinates = GetNetCoordinates(coordinates),
                        Target = target is { } targetEntity ? GetNetEntity(targetEntity) : null,
                        AltInteract = alt,
                        ActivateInWorld = command.Command == BodyControlCommand.ActivateWorld,
                        Action = command.Command switch
                        {
                            BodyControlCommand.Throw => RemoteControlInteractionAction.Throw,
                            BodyControlCommand.Pull => RemoteControlInteractionAction.TryPull,
                            BodyControlCommand.Shoot => RemoteControlInteractionAction.Shoot,
                            BodyControlCommand.MovePulledObject => RemoteControlInteractionAction.MovePulledObject,
                            _ => shoot ? RemoteControlInteractionAction.Shoot : RemoteControlInteractionAction.Interact,
                        },
                    }, hand);
                    break;
            }
        });
    }

    private EntityUid? ResolveTarget(NetEntity? target)
        => target is { } entity && TryGetEntity(entity, out var uid) && !TerminatingOrDeleted(uid.Value) ? uid : null;

    private bool CanControlAction(EntityUid body, EntityUid action, BodyControlAccess access)
    {
        return access.Allows("body.actions") && !TerminatingOrDeleted(action)
               && _controlActions.GetActions(body).Any(available => available.Owner == action)
               && _controlActions.GetEvent(action) is not OpenMindCollectiveEvent
               && (access.FullControl || access.Actions == null
                   || MetaData(action).EntityPrototype is { } prototype && access.Actions.Contains(prototype.ID));
    }

    [SubscribeLocalEvent]
    private void OnControlledMelee(Entity<BodyControlTargetComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!_handContext.IsExecuting(ent) && !IsNativeFullController(ent) && !HasExternalController(ent))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnControlledShot(Entity<BodyControlTargetComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!_handContext.IsExecuting(ent) && !IsNativeFullController(ent) && !HasExternalController(ent))
            args.Cancel();
    }

    private bool IsNativeFullController(EntityUid body)
        => TryGetControl(body, out var controlled, out var access) && controlled == body && access.FullControl;

    private bool HasExternalController(Entity<BodyControlTargetComponent> body)
        => body.Comp.Controllers.Any(controller => TryComp<BodyControlSourceComponent>(controller, out var source)
            && source.ExternallyDriven && TryGetControl(controller, out var controlled, out _) && controlled == body.Owner);

    private void CloseControlInterfaces(EntityUid actor)
        => _remoteControl.CloseControllerInterfaces(actor);

    [SubscribeLocalEvent]
    private void OnNativeActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (TryComp<BodyControlSourceComponent>(args.User, out var source) && source.Body == args.User
            && !source.ExternallyDriven
            && !_handContext.IsExecuting(args.User) && !source.Access.FullControl
            && _controlActions.GetEvent(ent) is not OpenMindCollectiveEvent)
            args.Cancelled = true;
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
        if (TryComp<HandsComponent>(body, out var hands))
            foreach (var hand in hands.SortedHands)
            {
                if (_handContext.AllowsHand(body, hand, access) && _controlHands.TryGetHeldItem((body, hands), hand, out var item))
                    entities.Add(item.Value);
            }
        foreach (var action in _controlActions.GetActions(body))
            if (CanControlAction(body, action.Owner, access))
                entities.Add(action.Owner);
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
