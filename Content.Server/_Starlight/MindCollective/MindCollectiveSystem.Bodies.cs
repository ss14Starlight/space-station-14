using System.Linq;
using Content.Shared._Starlight.MindCollective;
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
            || TerminatingOrDeleted(body) || HasComp<MindCollectiveObserverComponent>(body)
            || TryComp<MindCollectiveBodyComponent>(body, out var existing) && existing.Collective != collectiveId
            || _mind.TryGetMind(body, out var occupant, out _) && occupant != collective.OwnerMind && !collective.Minds.ContainsKey(occupant))
            return false;
        EnsureComp<MindCollectiveBodyComponent>(body).Collective = collectiveId;
        collective.Bodies.Add(body);
        SetCollectiveUi(body);
        return true;
    }

    public void UnregisterBody(EntityUid collectiveId, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || !collective.Bodies.Contains(body))
            return;
        if (!collective.Closing && _mind.TryGetMind(body, out var mind, out _) && collective.Minds.ContainsKey(mind))
            ReturnToMindspace(collectiveId, collective, mind);
        collective.Bodies.Remove(body);
        if (collective.ActiveBody == body)
            collective.ActiveBody = collective.Bodies.Count > 0 ? collective.Bodies.First() : null;
        DetachObservers(collectiveId, collective, body);
        if (!TerminatingOrDeleted(body))
        {
            _ui.CloseUi(body, MindCollectiveUiKey.Key);
            RemComp<MindCollectiveUiComponent>(body);
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
        if (collective.Closing || collective.Moving || !collective.Minds.TryGetValue(mind, out var observer)
            || collective.PendingMinds.Contains(mind) || !IsRegisteredBody(collectiveId, collective, target)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity is not { } source
            || source == target || source != observer && !IsRegisteredBody(collectiveId, collective, source))
            return false;
        if (_mind.TryGetMind(target, out var occupant, out _))
        {
            return mind == collective.OwnerMind && occupant != collective.OwnerMind
                   && collective.Minds.ContainsKey(occupant) && !collective.PendingMinds.Contains(occupant)
                   && (!TryComp<ActorComponent>(target, out var actor)
                       || Comp<MindComponent>(occupant).UserId == actor.PlayerSession.UserId);
        }
        return !HasComp<ActorComponent>(target);
    }

    /// <summary>Transfers only the requesting player, within their own collective.</summary>
    public bool TryEnterBody(EntityUid actor, EntityUid target)
    {
        if (!TryGetCollective(actor, out var id, out var collective)
            || !TryGetActorMind(actor, id, collective, out var mind) || !CanEnter(id, collective, mind, target))
            return false;
        var source = Comp<MindComponent>(mind).OwnedEntity!.Value;
        var occupied = _mind.TryGetMind(target, out var otherMind, out _);
        collective.Moving = true;
        try
        {
            if (occupied && !ReturnToMindspace(id, collective, otherMind))
                return false;
            CloseBodyUi(source);
            if (collective.Closing || !IsRegisteredBody(id, collective, target)
                || !TryComp<MindComponent>(mind, out var current) || current.OwnedEntity != source
                || _mind.TryGetMind(target, out _, out _) || HasComp<ActorComponent>(target))
                return false;
            _mind.TransferTo(mind, target, ghostCheckOverride: true, createGhost: false);
            if (Comp<MindComponent>(mind).OwnedEntity != target)
                return false;
            if (mind == collective.OwnerMind)
                collective.ActiveBody = target;
            if (IsRegisteredBody(id, collective, source))
            {
                if (occupied && !HasComp<ActorComponent>(source) && !_mind.TryGetMind(source, out _, out _))
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
        if (!TryGetCollective(actor, out var id, out var collective)
            || !TryGetActorMind(actor, id, collective, out var mind) || collective.Moving)
            return false;
        return ReturnToMindspace(id, collective, mind);
    }

    private bool ReturnToMindspace(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Closing || !collective.Minds.TryGetValue(mind, out var observer) || TerminatingOrDeleted(observer)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity == observer
            || collective.PendingMinds.Contains(mind)
            || consciousness.OwnedEntity is { } owned && (!collective.Bodies.Contains(owned)
                || !TryComp<MindCollectiveBodyComponent>(owned, out var carrier) || carrier.Collective != collectiveId)
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
        if (!TryGetCollective(actor, out var id, out var collective) || collective.Moving
            || !TryGetActorMind(actor, id, collective, out var mind) || mind != collective.OwnerMind || targetMind == mind)
            return false;
        return ReturnToMindspace(id, collective, targetMind);
    }

    private void CloseBodyUi(EntityUid body)
    {
        _ui.CloseUi(body, MindCollectiveUiKey.Key);
        RemComp<MindCollectiveUiComponent>(body);
        if (TryComp<MindCollectiveObserverComponent>(body, out var observer))
        {
            observer.OpenUiAt = TimeSpan.Zero;
            RemoveObserverViews(body);
        }
    }

    [SubscribeLocalEvent]
    private void OnBodyPlayerDetached(Entity<MindCollectiveBodyComponent> ent, ref PlayerDetachedEvent args)
        => CloseBodyUi(ent);

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnBodyTerminating(Entity<MindCollectiveBodyComponent> ent, ref EntityTerminatingEvent args)
        => UnregisterBody(ent.Comp.Collective, ent);

    [SubscribeLocalEvent]
    private void OnBodyShutdown(Entity<MindCollectiveBodyComponent> ent, ref ComponentShutdown args)
        => UnregisterBody(ent.Comp.Collective, ent);

    [SubscribeLocalEvent]
    private void OnCollectiveUiOpened(Entity<UserInterfaceComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is MindCollectiveUiKey && (args.Actor != ent.Owner
            || !TryGetCollective(ent, out var id, out var collective) || !TryGetActorMind(ent, id, collective, out _)))
            _ui.CloseUi(ent.Owner, args.UiKey, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnCollectiveUiClosed(Entity<UserInterfaceComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is MindCollectiveUiKey && args.Actor == ent.Owner
            && TryGetCollective(ent, out var id, out var collective)
            && TryGetActorMind(ent, id, collective, out var mind) && mind == collective.OwnerMind)
            CancelExpulsion(id);
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
            && TryGetCollective(ent, out var id, out var collective) && TryGetActorMind(ent, id, collective, out _))
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
}
