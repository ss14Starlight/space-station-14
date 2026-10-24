using System.Linq;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.BodyControl;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server._Starlight.BodyControl;

/// <summary>Routes authorized players' movement to one body without changing its mind or actor.</summary>
public sealed partial class BodyControlSystem : EntitySystem
{
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private InputSystem _input = default!;
    [Dependency] private NPCSystem _controlNpc = default!;
    [Dependency] private MobStateSystem _controlMobState = default!;

    private ulong _pressOrder;

    private static readonly MoveButtons[] Directions =
        [MoveButtons.Up, MoveButtons.Down, MoveButtons.Left, MoveButtons.Right];

    public override void Initialize()
    {
        base.Initialize();
        InitializeInteractions();
    }

    public override void Shutdown()
    {
        ShutdownInteractions();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        List<EntityUid>? expired = null;
        var query = EntityQueryEnumerator<BodyControlSourceComponent>();
        while (query.MoveNext(out var actor, out var source))
        {
            if (!source.ExternallyDriven)
                UpdateControlPresentation(actor, source);
            else if (source.Provider is not { } provider || TerminatingOrDeleted(provider)
                     || !TryGetControl(actor, out _, out _))
                (expired ??= new()).Add(actor);
        }
        if (expired != null)
            foreach (var actor in expired)
                StopControl(actor);
    }

    /// <summary>The caller must authorize this grant. An existing native player must be registered first.</summary>
    public bool TryStartControl(EntityUid actor, EntityUid body, BodyControlAccess access)
    {
        // The native actor already owns this body; a cooperative grant must preserve those full rights.
        if (actor == body && !access.FullControl)
            return false;
        if (TerminatingOrDeleted(actor) || TerminatingOrDeleted(body)
            || !TryComp<PhysicsComponent>(body, out var physics)
            || physics.BodyType is not (BodyType.KinematicController or BodyType.Kinematic)
            || !TryComp<ActorComponent>(actor, out var player) || player.PlayerSession.AttachedEntity != actor
            || !_mind.TryGetMind(actor, out var mind, out var consciousness) || consciousness.OwnedEntity != actor
            || consciousness.UserId != player.PlayerSession.UserId
            || HasComp<RelayInputMoverComponent>(actor) && !HasComp<BodyControlSourceComponent>(actor)
            || HasComp<RelayInputMoverComponent>(body))
            return false;
        if (TryComp<BodyControlSourceComponent>(actor, out var existing))
            return !existing.ExternallyDriven && existing.Body == body && UpdateAccess(actor, access);
        if (TryComp<BodyControlTargetComponent>(body, out var externalTarget) && externalTarget.ExternallyDriven)
            return false;
        if (HasComp<ActorComponent>(body) && actor != body
            && (!TryComp<BodyControlSourceComponent>(body, out var native) || native.Body != body))
            return false;
        if (HasComp<MovementRelayTargetComponent>(body) && !HasComp<BodyControlTargetComponent>(body)
            || actor != body && HasComp<BodyControlTargetComponent>(actor)
            || TryComp<BodyControlSourceComponent>(body, out var bodySource) && bodySource.Body != body)
            return false;

        var target = EnsureComp<BodyControlTargetComponent>(body);
        var source = AddComp<BodyControlSourceComponent>(actor);
        source.Body = body;
        source.Mind = mind;
        source.Access = access.Copy();
        InitializeControlHand(actor, source);
        ReadHeldInput(actor, source);
        if (actor != body)
            _mover.ResetMovementInput(actor);
        target.Controllers.Add(actor);
        Dirty(actor, source);
        Rebalance(body, target);
        UpdateControlPresentation(actor, source);
        return true;
    }

    public bool UpdateAccess(EntityUid actor, BodyControlAccess access)
    {
        if (!TryGetControl(actor, out var body, out _) || !TryComp<BodyControlSourceComponent>(actor, out var source))
            return false;
        if (source.ExternallyDriven)
            return false;
        if (actor == body && !access.FullControl)
            return false;
        var couldMove = source.Access.Allows("body.move");
        RemoveControlPresentation(actor);
        source.Access = access.Copy();
        InitializeControlHand(actor, source);
        UpdateControlPresentation(actor, source);
        if (couldMove != source.Access.Allows("body.move"))
        {
            source.Held = MoveButtons.None;
            source.PressOrder.Clear();
            if (source.Access.Allows("body.move"))
                ReadHeldInput(actor, source);
        }
        Dirty(actor, source);
        if (couldMove != source.Access.Allows("body.move"))
            Rebalance(body, Comp<BodyControlTargetComponent>(body));
        return true;
    }

    public bool IsControlling(EntityUid actor, EntityUid body)
        => TryGetControl(actor, out var target, out _) && target == body;

    /// <summary>Resolves a current grant using the real session and mind of the player entity.</summary>
    public bool TryGetControl(EntityUid actor, out EntityUid body, out BodyControlAccess access)
    {
        body = default;
        access = default!;
        if (TerminatingOrDeleted(actor) || !TryComp<BodyControlSourceComponent>(actor, out var source)
            || TerminatingOrDeleted(source.Body) || !TryComp<BodyControlTargetComponent>(source.Body, out var target)
            || !target.Controllers.Contains(actor) || !TryComp<ActorComponent>(actor, out var player)
            || player.PlayerSession.AttachedEntity != actor)
            return false;
        if (source.ExternallyDriven)
        {
            if (source.Provider is not { } provider || TerminatingOrDeleted(provider))
                return false;
        }
        else if (!_mind.TryGetMind(actor, out var mind, out var consciousness) || mind != source.Mind
                 || consciousness.OwnedEntity != actor || consciousness.UserId != player.PlayerSession.UserId)
            return false;
        body = source.Body;
        access = source.Access;
        return true;
    }

    public void StopControl(EntityUid actor)
        => StopControl(actor, detaching: false);

    private void StopControl(EntityUid actor, bool detaching)
    {
        if (!TryComp<BodyControlSourceComponent>(actor, out var source) || !source.Body.IsValid())
            return;
        if (source.ExternallyDriven)
        {
            ReleaseExclusiveControl(actor, source.Provider, notifyProvider: true);
            return;
        }
        CloseControlInterfaces(actor);
        RemoveControlPresentation(actor);
        var body = source.Body;
        source.Body = EntityUid.Invalid;
        source.Held = MoveButtons.None;
        source.PressOrder.Clear();
        RemoveRelay(actor, body);
        if (!TerminatingOrDeleted(actor) && source.LifeStage <= ComponentLifeStage.Running)
            RemComp<BodyControlSourceComponent>(actor);
        if (TryComp<BodyControlTargetComponent>(body, out var target))
        {
            target.Controllers.Remove(actor);
            // A native actor cannot bypass shared input arbitration by discarding just its own grant.
            if (actor == body && !detaching && HasComp<ActorComponent>(body))
            {
                foreach (var other in target.Controllers.ToArray())
                    StopControl(other);
            }
            Rebalance(body, target);
        }
        if (actor != body && !TerminatingOrDeleted(actor))
        {
            _mover.ResetMovementInput(actor);
            if (!detaching && HasComp<PhysicsComponent>(actor) && TryComp<ActorComponent>(actor, out var player)
                && player.PlayerSession.AttachedEntity == actor
                && _mind.TryGetMind(actor, out var currentMind, out var consciousness)
                && currentMind == source.Mind && consciousness.OwnedEntity == actor
                && consciousness.UserId == player.PlayerSession.UserId)
                _mover.ApplyMovementInput(actor, ReadHeldButtons(player.PlayerSession));
        }
    }

    private MoveButtons ReadHeldButtons(ICommonSession session)
    {
        var input = _input.GetInputStates(session);
        var buttons = MoveButtons.None;
        if (input.GetState(EngineKeyFunctions.MoveUp) == BoundKeyState.Down)
            buttons |= MoveButtons.Up;
        if (input.GetState(EngineKeyFunctions.MoveDown) == BoundKeyState.Down)
            buttons |= MoveButtons.Down;
        if (input.GetState(EngineKeyFunctions.MoveLeft) == BoundKeyState.Down)
            buttons |= MoveButtons.Left;
        if (input.GetState(EngineKeyFunctions.MoveRight) == BoundKeyState.Down)
            buttons |= MoveButtons.Right;
        if (input.GetState(EngineKeyFunctions.Walk) == BoundKeyState.Down)
            buttons |= MoveButtons.Walk;
        return buttons;
    }

    private void ReadHeldInput(EntityUid actor, BodyControlSourceComponent source)
    {
        if (!source.Access.Allows("body.move") || !TryComp<ActorComponent>(actor, out var player))
            return;
        var input = _input.GetInputStates(player.PlayerSession);
        RecordInput(source, MoveButtons.Up, input.GetState(EngineKeyFunctions.MoveUp) == BoundKeyState.Down);
        RecordInput(source, MoveButtons.Down, input.GetState(EngineKeyFunctions.MoveDown) == BoundKeyState.Down);
        RecordInput(source, MoveButtons.Left, input.GetState(EngineKeyFunctions.MoveLeft) == BoundKeyState.Down);
        RecordInput(source, MoveButtons.Right, input.GetState(EngineKeyFunctions.MoveRight) == BoundKeyState.Down);
        RecordInput(source, MoveButtons.Walk, input.GetState(EngineKeyFunctions.Walk) == BoundKeyState.Down);
    }

    private void RecordInput(BodyControlSourceComponent source, MoveButtons button, bool pressed)
    {
        if (pressed)
        {
            if ((source.Held & button) != 0)
                return;
            source.Held |= button;
            source.PressOrder[button] = ++_pressOrder;
        }
        else
        {
            source.Held &= ~button;
            source.PressOrder.Remove(button);
        }
    }

    [SubscribeLocalEvent]
    private void OnMoveInput(Entity<BodyControlSourceComponent> ent, ref BodyControlMoveInputEvent args)
    {
        if (ent.Comp.ExternallyDriven)
            return;
        if (!TryGetControl(ent, out var body, out var access))
        {
            args.Handled = true;
            StopControl(ent);
            return;
        }
        if (!access.Allows("body.move"))
        {
            args.Handled = true;
            return;
        }
        RecordInput(ent.Comp, args.Button, args.Pressed);
        var target = Comp<BodyControlTargetComponent>(body);
        if (target.ControllerCount <= 1)
            return; // The native mover and its one-to-one relay retain prediction.
        args.Handled = true;
        ApplyMovement(body, target);
    }

    private void Rebalance(EntityUid body, BodyControlTargetComponent target)
    {
        if (target.ExternallyDriven)
            return;
        foreach (var actor in target.Controllers.ToArray())
            RemoveRelay(actor, body);
        if (TerminatingOrDeleted(body) || target.LifeStage > ComponentLifeStage.Running)
            return;

        target.ControllerCount = target.Controllers.Count;
        target.SoleController = target.ControllerCount == 1 ? target.Controllers.First() : null;
        Dirty(body, target);
        EnsureComp<InputMoverComponent>(body);
        _mover.ResetMovementInput(body);
        if (target.ControllerCount == 0)
        {
            RestoreNpc(body, target);
            RemComp<BodyControlTargetComponent>(body);
            return;
        }
        PauseNpc(body, target);
        if (target.SoleController is { } sole && sole != body
            && Comp<BodyControlSourceComponent>(sole).Access.Allows("body.move"))
        {
            EnsureComp<InputMoverComponent>(sole);
            _mover.SetRelay(sole, body);
        }
        ApplyMovement(body, target);
    }

    private void RemoveRelay(EntityUid actor, EntityUid body)
    {
        if (TryComp<RelayInputMoverComponent>(actor, out var relay) && relay.RelayEntity == body
            && relay.LifeStage <= ComponentLifeStage.Running)
            RemComp<RelayInputMoverComponent>(actor);
    }

    private void PauseNpc(EntityUid body, BodyControlTargetComponent target)
    {
        if (!TryComp<HTNComponent>(body, out var htn))
            return;
        target.RestoreNpcAwake ??= _controlNpc.IsAwake(body, htn);
        if (_controlNpc.IsAwake(body, htn))
            _controlNpc.SleepNPC(body, htn);
    }

    private void RestoreNpc(EntityUid body, BodyControlTargetComponent target)
    {
        var wake = target.RestoreNpcAwake == true;
        target.RestoreNpcAwake = null;
        if (wake && !TerminatingOrDeleted(body) && !HasComp<ActorComponent>(body)
            && !_controlMobState.IsIncapacitated(body) && TryComp<HTNComponent>(body, out var htn))
            _controlNpc.WakeNPC(body, htn);
    }

    private void ApplyMovement(EntityUid body, BodyControlTargetComponent target)
    {
        if (target.SoleController is { } sole && TryComp<BodyControlSourceComponent>(sole, out var single))
        {
            _mover.ApplyMovementInput(body, single.Access.Allows("body.move") ? single.Held : MoveButtons.None);
            return;
        }
        MoveButtons buttons = MoveButtons.None;
        ulong horizontal = 0;
        ulong vertical = 0;
        foreach (var actor in target.Controllers)
        {
            if (!TryComp<BodyControlSourceComponent>(actor, out var source) || !source.Access.Allows("body.move"))
                continue;
            buttons |= source.Held & MoveButtons.Walk;
            foreach (var direction in Directions)
            {
                if (!source.PressOrder.TryGetValue(direction, out var order))
                    continue;
                var mask = direction is MoveButtons.Left or MoveButtons.Right
                    ? MoveButtons.Left | MoveButtons.Right : MoveButtons.Up | MoveButtons.Down;
                ref var latest = ref (direction is MoveButtons.Left or MoveButtons.Right ? ref horizontal : ref vertical);
                if (order <= latest)
                    continue;
                latest = order;
                buttons = (buttons & ~mask) | direction;
            }
        }
        _mover.ApplyMovementInput(body, buttons);
    }

    [SubscribeLocalEvent(after: [typeof(HTNSystem)])]
    private void OnSourceDetached(Entity<BodyControlSourceComponent> ent, ref PlayerDetachedEvent args)
    {
        if (ent.Comp.ExternallyDriven)
        {
            StopControl(ent, detaching: true);
            return;
        }
        // Stock NPC detach decides whether a vacated native body should wake before we pause it again.
        if (ent.Comp.Body == ent.Owner && TryComp<BodyControlTargetComponent>(ent.Owner, out var target)
            && TryComp<HTNComponent>(ent.Owner, out var htn) && _controlNpc.IsAwake(ent.Owner, htn))
            target.RestoreNpcAwake = true;
        StopControl(ent, detaching: true);
    }

    [SubscribeLocalEvent]
    private void OnSourceMindRemoved(Entity<BodyControlSourceComponent> ent, ref MindRemovedMessage args)
    {
        if (!ent.Comp.ExternallyDriven)
            StopControl(ent, detaching: true);
    }

    [SubscribeLocalEvent]
    private void OnSourceShutdown(Entity<BodyControlSourceComponent> ent, ref ComponentShutdown args)
        => StopControl(ent);

    [SubscribeLocalEvent]
    private void OnTargetShutdown(Entity<BodyControlTargetComponent> ent, ref ComponentShutdown args)
    {
        foreach (var actor in ent.Comp.Controllers.ToArray())
            StopControl(actor);
        if (ent.Comp.ExternallyDriven)
            return;
        if (!TerminatingOrDeleted(ent))
            _mover.ResetMovementInput(ent);
        RestoreNpc(ent, ent.Comp);
    }

    [SubscribeLocalEvent(after: [typeof(HTNSystem)])]
    private void OnTargetMobState(Entity<BodyControlTargetComponent> ent, ref MobStateChangedEvent args)
    {
        if (!ent.Comp.ExternallyDriven && ent.Comp.Controllers.Count > 0)
            PauseNpc(ent, ent.Comp);
    }

    [SubscribeLocalEvent(after: [typeof(HTNSystem)])]
    private void OnTargetDetached(Entity<BodyControlTargetComponent> ent, ref PlayerDetachedEvent args)
    {
        if (ent.Comp.ExternallyDriven || ent.Comp.Controllers.Count == 0)
            return;
        if (TryComp<HTNComponent>(ent, out var htn) && _controlNpc.IsAwake(ent, htn))
            ent.Comp.RestoreNpcAwake = true;
        PauseNpc(ent, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnTargetAttached(Entity<BodyControlTargetComponent> ent, ref PlayerAttachedEvent args)
    {
        if (ent.Comp.ExternallyDriven)
            return; // The provider preserves its existing direct-player takeover policy.
        if (ent.Comp.Controllers.Contains(ent.Owner))
            return;
        foreach (var actor in ent.Comp.Controllers.ToArray())
            StopControl(actor);
    }
}
