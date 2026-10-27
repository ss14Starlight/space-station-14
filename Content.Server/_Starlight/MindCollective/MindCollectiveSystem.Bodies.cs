using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Ghost;
using Content.Shared.Mind;
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
            || _mind.TryGetMind(body, out var occupant, out _) && !collective.Members.ContainsKey(occupant))
            return false;
        EnsureComp<MindCollectiveBodyComponent>(body).Collective = collectiveId;
        collective.Bodies.Add(body);
        if (_mind.TryGetMind(body, out var nativeMind, out _))
            RefreshNativeControl(collectiveId, collective, nativeMind);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public void UnregisterBody(EntityUid collectiveId, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || !collective.Bodies.Contains(body))
            return;
        StopBodyControllers(body);
        if (!collective.Closing && _mind.TryGetMind(body, out var mind, out _) && collective.Members.ContainsKey(mind))
            ReturnToMindspace(collectiveId, collective, mind);
        collective.Bodies.Remove(body);
        foreach (var member in collective.Members.Values)
            member.ControlAccess.Remove(body);
        if (collective.ActiveBody == body)
            collective.ActiveBody = collective.Bodies.Count > 0 ? collective.Bodies.First() : null;
        DetachObservers(collectiveId, collective, body);
        if (!TerminatingOrDeleted(body))
            RemComp<MindCollectiveBodyComponent>(body);
        if (!collective.Closing)
            UpdateUi(collectiveId, collective);
    }

    private bool IsRegisteredBody(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid body)
        => !TerminatingOrDeleted(body) && collective.Bodies.Contains(body)
           && TryComp<MindCollectiveBodyComponent>(body, out var member) && member.Collective == collectiveId;

    private bool CanEnter(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind, EntityUid target)
    {
        if (collective.Closing || collective.Moving || !collective.Members.TryGetValue(mind, out var member)
            || member.Pending || member.Leaving || !IsRegisteredBody(collectiveId, collective, target)
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
        if (collective.Closing || !collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving
            || TerminatingOrDeleted(member.Observer) || !TryComp<MindComponent>(mind, out var consciousness)
            || consciousness.OwnedEntity == member.Observer || HasComp<ActorComponent>(member.Observer)
            || _mind.TryGetMind(member.Observer, out _, out _))
            return false;
        var observer = member.Observer;
        var previous = consciousness.OwnedEntity;
        _mind.TransferTo(mind, observer, ghostCheckOverride: true, createGhost: false);
        if (consciousness.OwnedEntity != observer)
            return false;
        if (previous is { } body)
        {
            RaiseLocalEvent(body, new MindCollectiveBodyControlChangedEvent(mind, null));
            if (collective.Closing || TerminatingOrDeleted(observer)
                || !collective.Members.TryGetValue(mind, out var current) || current != member)
                return false;
            if (IsRegisteredBody(collectiveId, collective, body))
                Observe(observer, body);
        }
        UpdateObserver(observer, collective);
        OpenMenu(collectiveId, collective, mind);
        UpdateUi(collectiveId, collective);
        return true;
    }

    public bool TryRecallMember(EntityUid actor, EntityUid targetMind)
    {
        if (!TryGetActorCollective(actor, out var id, out var collective, out var mind) || collective.Moving
            || !CanManageMember(mind, targetMind, "mind-collective.recall"))
            return false;
        if (collective.Members.TryGetValue(targetMind, out var member) && HasComp<BodyControlSourceComponent>(member.Observer))
        {
            _bodyControl.StopControl(member.Observer);
            UpdateUi(id, collective);
            return true;
        }
        return ReturnToMindspace(id, collective, targetMind);
    }

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnBodyTerminating(Entity<MindCollectiveBodyComponent> ent, ref EntityTerminatingEvent args)
        => UnregisterBody(ent.Comp.Collective, ent);

    [SubscribeLocalEvent]
    private void OnBodyShutdown(Entity<MindCollectiveBodyComponent> ent, ref ComponentShutdown args)
        => UnregisterBody(ent.Comp.Collective, ent);

    private bool CanControlBody(EntityUid id, MindCollectiveComponent collective, EntityUid mind, EntityUid actor, EntityUid body)
        => !collective.Moving && HasComp<MindCollectiveObserverComponent>(actor)
           && IsRegisteredBody(id, collective, body) && HasPermission(mind, "body.observe", body)
           && TryGetControlAccess(mind, body, out _)
           && (!HasComp<ActorComponent>(body) || _mind.TryGetMind(body, out var occupant, out _)
               && collective.Members.TryGetValue(occupant, out var member) && !member.Pending && !member.Leaving);

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
        var nativeAccess = _mind.TryGetMind(body, out var nativeMind, out _)
            ? GetMemberControlAccess(collective, nativeMind, body) : new BodyControlAccess { FullControl = true };
        if (addNative && !_bodyControl.TryStartControl(body, body, nativeAccess))
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
}
