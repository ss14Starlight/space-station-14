using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.UserInterface;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    /// <summary>Registers a prepared body. Registering it never recruits or displaces its current occupant.</summary>
    public bool RegisterBody(EntityUid collectiveId, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || TerminatingOrDeleted(body) || HasComp<MindCollectiveObserverComponent>(body) || HasComp<GhostComponent>(body)
            || TryComp<MindCollectiveBodyComponent>(body, out var existing) && existing.Collective != collectiveId
            || _mind.TryGetMind(body, out var occupant, out _) && !collective.Minds.ContainsKey(occupant))
            return false;
        EnsureComp<MindCollectiveBodyComponent>(body).Collective = collectiveId;
        collective.Bodies.Add(body);
        SetCollectiveUi(body);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public void UnregisterBody(EntityUid collectiveId, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || !collective.Bodies.Contains(body))
            return;
        StopBodyControllers(body);
        if (!collective.Closing && _mind.TryGetMind(body, out var mind, out _) && collective.Minds.ContainsKey(mind))
            ReturnToMindspace(collectiveId, collective, mind);
        collective.Bodies.Remove(body);
        if (collective.ActiveBody == body)
            collective.ActiveBody = collective.Bodies.Count > 0 ? collective.Bodies.First() : null;
        DetachObservers(collectiveId, collective, body);
        if (!TerminatingOrDeleted(body))
        {
            CloseBodyUi(body, collectiveId);
            RemComp<MindCollectiveBodyComponent>(body);
        }
        if (!collective.Closing)
            UpdateUi(collectiveId, collective);
    }

    private bool IsRegisteredBody(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid body)
        => !TerminatingOrDeleted(body) && collective.Bodies.Contains(body)
           && TryComp<MindCollectiveBodyComponent>(body, out var member) && member.Collective == collectiveId;

    private bool CanEnter(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind, EntityUid target)
    {
        if (collective.Closing || collective.Moving || !collective.Minds.ContainsKey(mind)
            || collective.PendingMinds.Contains(mind) || !IsRegisteredBody(collectiveId, collective, target)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity is not { } source
            || source == target
            || !HasPermission(mind, "body.enter", target))
            return false;
        if (_mind.TryGetMind(target, out var occupant, out _))
        {
            return HasPermission(mind, "body.takeover", target) && CanManageMember(mind, occupant, "body.takeover")
                   && (!TryComp<ActorComponent>(target, out var actor)
                       || Comp<MindComponent>(occupant).UserId == actor.PlayerSession.UserId);
        }
        return !HasComp<ActorComponent>(target);
    }

    /// <summary>Transfers only the requesting player, within their own collective.</summary>
    public bool TryEnterBody(EntityUid actor, EntityUid target)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind)
            || !CanEnter(id, collective, mind, target))
            return false;
        var source = Comp<MindComponent>(mind).OwnedEntity!.Value;
        var occupied = _mind.TryGetMind(target, out var otherMind, out _);
        collective.Moving = true;
        try
        {
            if (occupied && !ReturnToMindspace(id, collective, otherMind))
                return false;
            StopBodyControllers(target);
            CloseBodyUi(source);
            if (collective.Closing || !IsRegisteredBody(id, collective, target)
                || !TryComp<MindComponent>(mind, out var current) || current.OwnedEntity != source
                || _mind.TryGetMind(target, out _, out _) || HasComp<ActorComponent>(target))
                return false;
            _mind.TransferTo(mind, target, ghostCheckOverride: true, createGhost: false);
            if (Comp<MindComponent>(mind).OwnedEntity != target)
                return false;
            collective.ActiveBody = target;
            if (IsRegisteredBody(id, collective, source))
            {
                if (occupied && HasPermission(otherMind, "body.enter", source)
                    && !HasComp<ActorComponent>(source) && !_mind.TryGetMind(source, out _, out _))
                {
                    _mind.TransferTo(otherMind, source, ghostCheckOverride: true, createGhost: false);
                    EnsureControlAction(otherMind);
                    RaiseLocalEvent(source, new MindCollectiveBodyControlChangedEvent(mind, otherMind));
                }
                else
                    RaiseLocalEvent(source, new MindCollectiveBodyControlChangedEvent(mind, null));
            }
            EnsureControlAction(mind);
            RaiseLocalEvent(target, new MindCollectiveBodyControlChangedEvent(occupied ? otherMind : null, mind));
        }
        finally
        {
            collective.Moving = false;
        }
        UpdateUi(id, collective);
        return true;
    }

    public bool TryLeaveBody(EntityUid actor)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind) || collective.Moving)
            return false;
        if (HasComp<MindCollectiveObserverComponent>(actor) && TryComp<BodyControlSourceComponent>(actor, out var control))
        {
            var body = control.Body;
            _bodyControl.StopControl(actor);
            Observe(actor, body);
            UpdateUi(id, collective);
            return true;
        }
        return ReturnToMindspace(id, collective, mind);
    }

    private bool ReturnToMindspace(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Closing || !collective.Minds.TryGetValue(mind, out var observer) || TerminatingOrDeleted(observer)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity == observer
            || collective.PendingMinds.Contains(mind)
            || HasComp<ActorComponent>(observer) || _mind.TryGetMind(observer, out _, out _))
            return false;
        var previous = consciousness.OwnedEntity;
        if (previous is { } source)
            CloseBodyUi(source);
        _mind.TransferTo(mind, observer, ghostCheckOverride: true, createGhost: false);
        if (previous is { } body)
        {
            RaiseLocalEvent(body, new MindCollectiveBodyControlChangedEvent(mind, null));
            if (collective.Closing || TerminatingOrDeleted(observer)
                || !collective.Minds.TryGetValue(mind, out var currentObserver) || currentObserver != observer)
                return false;
            if (IsRegisteredBody(collectiveId, collective, body))
                Observe(observer, body);
        }
        EnsureControlAction(mind);
        UpdateObserver(observer, collective);
        Comp<MindCollectiveObserverComponent>(observer).OpenUiAt = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public bool TryRecallMember(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind) || collective.Moving
            || !CanManageMember(mind, targetMind, "mind-collective.recall"))
            return false;
        if (collective.Minds.TryGetValue(targetMind, out var observer) && HasComp<BodyControlSourceComponent>(observer))
        {
            _bodyControl.StopControl(observer);
            UpdateUi(id, collective);
            return true;
        }
        return ReturnToMindspace(id, collective, targetMind);
    }

    private void CloseBodyUi(EntityUid body, EntityUid? collectiveId = null)
    {
        if (collectiveId != null && _mind.TryGetMind(body, out var mind, out _)
            && TryComp<MindCollectiveMemberComponent>(mind, out var member) && member.Collective != collectiveId)
            return;
        _ui.CloseUi(body, MindCollectiveUiKey.Key);
        RemComp<MindCollectiveUiComponent>(body);
        if (TryComp<MindCollectiveObserverComponent>(body, out var observer))
        {
            _bodyControl.StopControl(body);
            observer.OpenUiAt = TimeSpan.Zero;
            RemoveObserverViews(body);
        }
    }

    [SubscribeLocalEvent]
    private void OnMemberPlayerDetached(Entity<MindCollectiveUiComponent> ent, ref PlayerDetachedEvent args)
        => CloseBodyUi(ent);

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnBodyTerminating(Entity<MindCollectiveBodyComponent> ent, ref EntityTerminatingEvent args)
        => UnregisterBody(ent.Comp.Collective, ent);

    [SubscribeLocalEvent]
    private void OnBodyShutdown(Entity<MindCollectiveBodyComponent> ent, ref ComponentShutdown args)
        => UnregisterBody(ent.Comp.Collective, ent);

    [SubscribeLocalEvent]
    private void OnMemberUiOpened(Entity<MindCollectiveUiComponent> ent, ref BoundUIOpenedEvent args)
        => OnCollectiveUiOpened(ent, args);

    private void OnCollectiveUiOpened(EntityUid entity, BoundUIOpenedEvent args)
    {
        if (args.UiKey is MindCollectiveUiKey && (args.Actor != entity
            || !TryGetActorCollective(entity, out _, out _, out _)))
            _ui.CloseUi(entity, args.UiKey, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnMemberUiClosed(Entity<MindCollectiveUiComponent> ent, ref BoundUIClosedEvent args)
        => OnCollectiveUiClosed(ent, args);

    private void OnCollectiveUiClosed(EntityUid entity, BoundUIClosedEvent args)
    {
        if (args.UiKey is MindCollectiveUiKey && args.Actor == entity
            && TryGetActorCollective(entity, out var id, out _, out var mind))
            CancelExpulsion(id, mind);
    }

    [SubscribeLocalEvent]
    private void OnEnterMessage(Entity<UserInterfaceComponent> ent, ref EnterCollectiveBodyMessage args)
    {
        if (args.Actor == ent.Owner)
            TryEnterBody(ent, GetEntity(args.Body));
    }

    [SubscribeLocalEvent]
    private void OnLeaveMessage(Entity<UserInterfaceComponent> ent, ref LeaveCollectiveBodyMessage args)
    {
        if (args.Actor == ent.Owner)
            TryLeaveBody(ent);
    }

    [SubscribeLocalEvent]
    private void OnRecallMessage(Entity<UserInterfaceComponent> ent, ref RecallCollectiveMemberMessage args)
    {
        if (args.Actor == ent.Owner)
            TryRecallMember(ent, GetEntity(args.Mind));
    }

    [SubscribeLocalEvent]
    private void OnObserveMessage(Entity<UserInterfaceComponent> ent, ref ObserveCollectiveBodyMessage args)
    {
        if (args.Actor == ent.Owner && HasComp<MindCollectiveObserverComponent>(ent)
            && TryGetActorCollective(ent, out _, out _, out _))
            Observe(ent, GetEntity(args.Body));
    }

    [SubscribeLocalEvent]
    private void OnExpelMessage(Entity<UserInterfaceComponent> ent, ref ExpelCollectiveMemberMessage args)
    {
        if (args.Actor != ent.Owner)
            return;
        if (args.Request == 0)
            RequestExpulsion(ent, GetEntity(args.Mind));
        else
            RespondExpulsion(ent, GetEntity(args.Mind), args.Request, args.Confirmed);
    }

    private bool CanControlBody(EntityUid id, MindCollectiveComponent collective, EntityUid mind, EntityUid actor, EntityUid body)
        => !collective.Moving && HasComp<MindCollectiveObserverComponent>(actor)
           && IsRegisteredBody(id, collective, body) && HasPermission(mind, "body.observe", body)
           && TryGetControlAccess(mind, body, out _)
           && (!HasComp<ActorComponent>(body) || _mind.TryGetMind(body, out var occupant, out _)
               && collective.Minds.ContainsKey(occupant) && !collective.PendingMinds.Contains(occupant));

    /// <summary>Grants cooperative control without moving the participant's mind or displacing an occupant.</summary>
    public bool TryControlBody(EntityUid actor, EntityUid body)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind)
            || !CanControlBody(id, collective, mind, actor, body) || !TryGetControlAccess(mind, body, out var access))
            return false;
        if (_bodyControl.IsControlling(actor, body))
        {
            _bodyControl.StopControl(actor);
            UpdateUi(id, collective);
            return true;
        }
        if (!Observe(actor, body))
            return false;
        // The physical occupant is also an input source, so its commands follow the same arbitration rules.
        var addNative = HasComp<ActorComponent>(body) && !_bodyControl.IsControlling(body, body);
        if (addNative && !_bodyControl.TryStartControl(body, body, new BodyControlAccess { FullControl = true }))
            return false;
        if (!_bodyControl.TryStartControl(actor, body, access))
        {
            if (addNative)
                _bodyControl.StopControl(body);
            return false;
        }
        UpdateUi(id, collective);
        return true;
    }

    private void StopBodyControllers(EntityUid body)
    {
        if (TryComp<BodyControlTargetComponent>(body, out var target))
        {
            foreach (var source in target.Controllers.ToArray())
                _bodyControl.StopControl(source);
        }
    }

    [SubscribeLocalEvent]
    private void OnControlMessage(Entity<UserInterfaceComponent> ent, ref ControlCollectiveBodyMessage args)
    {
        if (args.Actor == ent.Owner)
            TryControlBody(ent, GetEntity(args.Body));
    }
}
