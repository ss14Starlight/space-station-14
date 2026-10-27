using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.MindCollective;

/// <summary>Membership and control of explicitly registered bodies, independently of their species or abilities.</summary>
public sealed partial class MindCollectiveSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private GhostSystem _ghosts = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionContainerSystem _actionContainers = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const string ControlAction = "ActionMindCollective";

    public EntityUid CreateCollective(EntProtoId prototype, string? name = null)
    {
        var id = Spawn(prototype);
        EnsureComp<MindCollectiveComponent>(id);
        if (name != null)
            _metadata.SetEntityName(id, name);
        return id;
    }

    /// <summary>Creates an independent group. The caller decides when it should be dissolved.</summary>
    public EntityUid? CreateCollective(EntityUid initialMind, EntityUid body)
    {
        if (!TryComp<MindComponent>(initialMind, out var mind) || mind.OwnedEntity != body
            || HasComp<MindCollectiveMemberComponent>(initialMind) || HasComp<MindCollectiveBodyComponent>(body)
            || TerminatingOrDeleted(body))
            return null;

        TryComp<MindCollectiveCreatorComponent>(body, out var creator);
        var id = CreateCollective(creator?.CollectivePrototype ?? "MindCollective");
        var collective = Comp<MindCollectiveComponent>(id);
        if (creator != null)
        {
            creator.Collective = id;
            collective.ObserverPrototype = creator.ObserverPrototype;
            collective.VacancyPrototype = creator.VacancyPrototype;
            collective.JoinTitle = creator.JoinTitle;
            collective.JoinDescription = creator.JoinDescription;
        }
        var member = AddMember(id, collective, initialMind, body);
        EnsureDefaultRole(id, collective, initialMind);
        AssignRole(id, initialMind, collective.InitialRole);
        if (HasComp<GhostComponent>(body))
            _mind.TransferTo(initialMind, member.Observer, ghostCheckOverride: true, createGhost: false);
        else if (RegisterBody(id, body))
            collective.ActiveBody = body;
        EnsureControlAction(initialMind);
        return id;
    }

    [SubscribeLocalEvent]
    private void OnCreatorStartup(Entity<MindCollectiveCreatorComponent> ent, ref ComponentStartup args)
        => InitializeCreator(ent);

    [SubscribeLocalEvent]
    private void OnCreatorMindAdded(Entity<MindCollectiveCreatorComponent> ent, ref MindAddedMessage args)
        => InitializeCreator(ent);

    private void InitializeCreator(Entity<MindCollectiveCreatorComponent> ent)
    {
        if (ent.Comp.Collective == null && _mind.TryGetMind(ent, out var mind, out _))
            CreateCollective(mind, ent);
    }

    public bool TryGetCollective(EntityUid entity, out EntityUid collectiveId, out MindCollectiveComponent collective)
    {
        collectiveId = default;
        collective = default!;
        if (HasComp<MindCollectiveComponent>(entity))
            collectiveId = entity;
        else if (TryComp<MindCollectiveMemberComponent>(entity, out var member))
            collectiveId = member.Collective;
        else if (TryComp<MindCollectiveObserverComponent>(entity, out var observer))
            collectiveId = observer.Collective;
        else if (TryComp<MindCollectiveBodyComponent>(entity, out var body))
            collectiveId = body.Collective;
        else
            return false;
        return TryComp(collectiveId, out collective!) && !collective.Closing;
    }

    public bool IsAccepted(EntityUid entity, MindCollectiveComponent collective)
        => !TerminatingOrDeleted(entity) && _mind.TryGetMind(entity, out var mind, out var consciousness)
           && consciousness.OwnedEntity == entity
           && collective.Members.TryGetValue(mind, out var member) && !member.Pending && !member.Leaving;

    private bool TryGetActorCollective(EntityUid actor, out EntityUid id, out MindCollectiveComponent collective,
        out EntityUid mind)
    {
        id = default;
        collective = default!;
        mind = default;
        return _mind.TryGetMind(actor, out var current, out _)
               && TryGetCollective(current, out id, out collective)
               && TryGetActorMind(actor, id, collective, out mind);
    }

    private bool TryGetActorMind(EntityUid actor, EntityUid collectiveId, MindCollectiveComponent collective, out EntityUid mind)
    {
        mind = default;
        return TryComp<ActorComponent>(actor, out var player)
               && player.PlayerSession.AttachedEntity == actor
               && _mind.TryGetMind(actor, out mind, out var consciousness)
               && consciousness.UserId == player.PlayerSession.UserId
               && IsAccepted(actor, collective)
               && TryComp<MindCollectiveMemberComponent>(mind, out var member) && member.Collective == collectiveId;
    }

    private MindCollectiveMemberState AddMember(EntityUid id, MindCollectiveComponent collective, EntityUid mind,
        EntityUid? body, bool pending = false)
    {
        var member = new MindCollectiveMemberState(CreateObserver(id, collective, mind, body)) { Pending = pending };
        collective.Members.Add(mind, member);
        EnsureComp<MindCollectiveMemberComponent>(mind).Collective = id;
        return member;
    }

    private EntityUid CreateObserver(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind, EntityUid? body)
    {
        var observer = body is { } target && !TerminatingOrDeleted(target)
            ? Spawn(collective.ObserverPrototype, _transform.GetMoverCoordinates(target))
            : Spawn(collective.ObserverPrototype);
        Comp<MindCollectiveObserverComponent>(observer).Collective = collectiveId;
        _metadata.SetEntityName(observer, Comp<MindComponent>(mind).CharacterName ?? Name(mind));
        return observer;
    }

    private void EnsureControlAction(EntityUid mind)
    {
        if (!TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity is not { } entity
            || !TryGetCollective(mind, out _, out var collective) || !IsAccepted(entity, collective))
            return;
        var member = collective.Members[mind];
        if (member.Action is not { } existing || TerminatingOrDeleted(existing))
            member.Action = _actionContainers.AddAction(mind, ControlAction);
        if (member.Action is not { } actionId || _actions.GetAction(actionId) is not { } action)
            return;
        if (action.Comp.AttachedEntity != entity)
            _actions.GrantContainedAction(entity, mind, actionId);
        _actions.SetEnabled(action.AsNullable(), true);
    }

    [SubscribeLocalEvent]
    private void OnOpenCollective(OpenMindCollectiveEvent args)
    {
        if (args.Handled || !TryGetActorCollective(args.Performer, out var id, out var collective, out var mind))
            return;
        args.Handled = true;
        OpenMenu(id, collective, mind);
    }

    [SubscribeLocalEvent]
    private void OnMemberMindAdded(Entity<MindCollectiveMemberComponent> ent, ref MindGotAddedEvent args)
    {
        if (!TryGetCollective(ent, out var id, out var collective))
            return;
        EnsureControlAction(ent);
        RefreshNativeControl(id, collective, ent);
        UpdateUi(id, collective);
    }

    [SubscribeLocalEvent]
    private void OnMemberMindRemoved(Entity<MindCollectiveMemberComponent> ent, ref MindGotRemovedEvent args)
    {
        if (!TryGetCollective(ent, out var id, out var collective)
            || !collective.Members.TryGetValue(ent, out var member))
            return;
        if (member.Action is { } action)
            _actions.RemoveAction(action);
        if (HasComp<MindCollectiveObserverComponent>(args.Container))
        {
            _bodyControl.StopControl(args.Container);
            RemoveObserverViews(args.Container);
        }
        if (args.TransferEntity == null && !member.Pending && !member.Leaving)
            QueueLocalEvent(new CollectiveMindDetachedEvent(id, ent));
        UpdateUi(id, collective);
    }

    // Mind removal is raised midway through TransferTo; resolve unowned minds after that operation completes.
    [SubscribeLocalEvent]
    private void OnMindDetached(CollectiveMindDetachedEvent args)
    {
        if (TryComp<MindCollectiveComponent>(args.Collective, out var collective)
            && TryComp<MindComponent>(args.Mind, out var mind) && mind.OwnedEntity == null)
            ReturnToMindspace(args.Collective, collective, args.Mind);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<MindCollectiveComponent>();
        while (query.MoveNext(out var id, out var collective))
        {
            if (collective.Closing || _timing.CurTime < collective.NextUpdate)
                continue;
            collective.NextUpdate = _timing.CurTime + TimeSpan.FromMilliseconds(250);
            UpdateMembership(id, collective);
            foreach (var member in collective.Members.Values.ToArray())
            {
                if (!member.Pending && !member.Leaving && !TerminatingOrDeleted(member.Observer))
                    UpdateObserver(member.Observer, collective);
            }
        }
    }

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnCollectiveTerminating(Entity<MindCollectiveComponent> ent, ref EntityTerminatingEvent args)
        => Disband(ent);

    [SubscribeLocalEvent]
    private void OnCollectiveShutdown(Entity<MindCollectiveComponent> ent, ref ComponentShutdown args)
        => Disband(ent);

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        var query = EntityQueryEnumerator<MindCollectiveComponent>();
        while (query.MoveNext(out var id, out _))
            Disband(id);
    }

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnMemberTerminating(Entity<MindCollectiveMemberComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp<MindCollectiveComponent>(ent.Comp.Collective, out var collective) && !collective.Closing)
            RemoveMember(ent.Comp.Collective, ent, createVacancy: false);
    }

    [SubscribeLocalEvent(before: [typeof(Content.Server.Mind.MindSystem)])]
    private void OnObserverTerminating(Entity<MindCollectiveObserverComponent> ent, ref EntityTerminatingEvent args)
        => RepairObserver(ent);

    private void RepairObserver(Entity<MindCollectiveObserverComponent> ent)
    {
        RemoveObserverViews(ent);
        if (!TryComp<MindCollectiveComponent>(ent.Comp.Collective, out var collective) || collective.Closing)
            return;
        collective.VacantObservers.Remove(ent);
        var entry = collective.Members.FirstOrDefault(pair => pair.Value.Observer == ent.Owner);
        if (entry.Value == null || entry.Value.Leaving)
            return;
        var (mind, member) = entry;
        if (member.Pending || TerminatingOrDeleted(mind))
        {
            RemoveMember(ent.Comp.Collective, mind, createVacancy: false);
            return;
        }
        member.Observer = CreateObserver(ent.Comp.Collective, collective, mind, collective.ActiveBody);
        RefreshMemberPermissions(ent.Comp.Collective, collective, mind);
        if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity == ent.Owner)
        {
            _mind.TransferTo(mind, member.Observer, ghostCheckOverride: true, createGhost: false);
            OpenMenu(ent.Comp.Collective, collective, mind);
        }
        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    public void Disband(EntityUid collectiveId)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing)
            return;
        collective.Closing = true;
        foreach (var mind in collective.Members.Keys.ToArray())
            RemoveMember(collectiveId, mind, createVacancy: false, disposition: MindCollectiveDepartureDisposition.Detach);
        foreach (var observer in collective.VacantObservers.ToArray())
        {
            RemoveObserverViews(observer);
            QueueDel(observer);
        }
        collective.VacantObservers.Clear();
        foreach (var body in collective.Bodies.ToArray())
        {
            StopBodyControllers(body);
            if (!TerminatingOrDeleted(body))
                RemComp<MindCollectiveBodyComponent>(body);
        }
        collective.Bodies.Clear();
        if (!TerminatingOrDeleted(collectiveId))
            QueueDel(collectiveId);
    }

    private sealed class CollectiveMindDetachedEvent(EntityUid collective, EntityUid mind) : EntityEventArgs
    {
        public readonly EntityUid Collective = collective;
        public readonly EntityUid Mind = mind;
    }
}
