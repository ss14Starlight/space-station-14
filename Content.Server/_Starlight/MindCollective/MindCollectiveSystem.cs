using System.Linq;
using Content.Server.Ghost;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
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
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionContainerSystem _actionContainers = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
        collective.ActiveBody = body;
        if (creator != null)
        {
            creator.Collective = id;
            collective.ObserverPrototype = creator.ObserverPrototype;
            collective.VacancyPrototype = creator.VacancyPrototype;
            collective.JoinTitle = creator.JoinTitle;
            collective.JoinDescription = creator.JoinDescription;
        }
        CreateObserver(id, collective, initialMind, null);
        EnsureDefaultRole(id, collective, initialMind);
        AssignRole(id, initialMind, collective.InitialRole);
        RegisterBody(id, body);
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
    {
        return !TerminatingOrDeleted(entity) && _mind.TryGetMind(entity, out var mind, out var consciousness)
               && consciousness.OwnedEntity == entity
               && collective.Minds.TryGetValue(mind, out var observer)
               && !collective.PendingMinds.Contains(mind)
               && (entity == observer || collective.Bodies.Contains(entity)
                   && TryComp<MindCollectiveBodyComponent>(entity, out var body)
                   && TryComp<MindCollectiveComponent>(body.Collective, out var group) && group == collective);
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

    private EntityUid CreateObserver(EntityUid collectiveId, MindCollectiveComponent collective, EntityUid mind, EntityUid? body)
    {
        var observer = body is { } target && !TerminatingOrDeleted(target)
            ? Spawn(collective.ObserverPrototype, Transform(target).Coordinates)
            : Spawn(collective.ObserverPrototype);
        Comp<MindCollectiveObserverComponent>(observer).Collective = collectiveId;
        collective.Minds.Add(mind, observer);
        EnsureComp<MindCollectiveMemberComponent>(mind).Collective = collectiveId;
        _metadata.SetEntityName(observer, Comp<MindComponent>(mind).CharacterName ?? Name(mind));
        SetCollectiveUi(observer);
        if (body is { } viewed && collective.Bodies.Contains(viewed))
            Observe(observer, viewed);
        return observer;
    }

    private void SetCollectiveUi(EntityUid entity)
    {
        EnsureComp<UserInterfaceComponent>(entity);
        _ui.SetUi(entity, MindCollectiveUiKey.Key,
            new InterfaceData("MindCollectiveBoundUserInterface", interactionRange: 0, requireInputValidation: false));
    }

    private void EnsureControlAction(EntityUid mind)
    {
        if (!TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity is not { } entity
            || !TryGetCollective(mind, out _, out var collective) || !IsAccepted(entity, collective))
            return;
        EntityUid? action = null;
        if (TryComp<ActionsContainerComponent>(mind, out var container))
        {
            foreach (var candidate in container.Container.ContainedEntities)
            {
                if (!TerminatingOrDeleted(candidate) && MetaData(candidate).EntityPrototype?.ID == ControlAction)
                {
                    action = candidate;
                    break;
                }
            }
        }
        action ??= _actionContainers.AddAction(mind, ControlAction);
        if (action is not { } actionId || _actions.GetAction(actionId) is not { } current)
            return;
        if (current.Comp.AttachedEntity != entity || !_actions.GetActions(entity).Any(a => a.Owner == actionId))
            _actions.GrantContainedAction(entity, mind, actionId);
        _actions.SetEnabled(current.AsNullable(), true);
    }

    [SubscribeLocalEvent]
    private void OnOpenBody(Entity<MindCollectiveBodyComponent> ent, ref OpenMindCollectiveEvent args)
        => OpenUi(ent, args);

    [SubscribeLocalEvent]
    private void OnOpenObserver(Entity<MindCollectiveObserverComponent> ent, ref OpenMindCollectiveEvent args)
        => OpenUi(ent, args);

    private void OpenUi(EntityUid actor, OpenMindCollectiveEvent args)
    {
        if (args.Handled || args.Performer != actor || !TryGetCollective(actor, out var id, out var collective)
            || !TryGetActorMind(actor, id, collective, out _))
            return;
        args.Handled = true;
        UpdateUi(id, collective);
        _ui.OpenUi(actor, MindCollectiveUiKey.Key, actor);
    }

    private void UpdateUi(EntityUid collectiveId, MindCollectiveComponent collective)
    {
        foreach (var mind in collective.Minds.Keys)
        {
            if (collective.PendingMinds.Contains(mind) || !TryComp<MindComponent>(mind, out var consciousness)
                || consciousness.OwnedEntity is not { } actor || TerminatingOrDeleted(actor) || !IsAccepted(actor, collective))
                continue;
            var canManage = HasPermission(mind, "mind-collective.expel") || HasPermission(mind, "mind-collective.roles");
            var bodies = new List<MindCollectiveBodyEntry>();
            foreach (var body in collective.Bodies)
            {
                if (!IsRegisteredBody(collectiveId, collective, body))
                    continue;
                var occupied = _mind.TryGetMind(body, out var occupant, out var occupiedMind);
                bodies.Add(new(GetNetEntity(body), Name(body), occupied ? occupiedMind!.CharacterName ?? Name(body) : null,
                    CanEnter(collectiveId, collective, mind, body),
                    occupied && CanManageMember(mind, occupant, "mind-collective.recall"),
                    occupied ? GetNetEntity(occupant) : null,
                    CanControlBody(collectiveId, collective, mind, actor, body), _bodyControl.IsControlling(actor, body)));
            }
            var members = collective.Minds.Keys.Where(m => !collective.PendingMinds.Contains(m))
                .Select(m => new MindCollectiveMemberEntry(GetNetEntity(m),
                    TryComp<MindComponent>(m, out var member) ? member.CharacterName ?? Name(m) : Name(m),
                    CanManageMember(mind, m, "mind-collective.expel"),
                    _rolePrototypes.EnumeratePrototypes<MindCollectiveRolePrototype>()
                        .Where(role => collective.Roles.TryGetValue(m, out var roles) && roles.ContainsKey(role.ID)
                            || HasPermission(mind, "mind-collective.roles") && role.Priority <= RolePriority(collective, mind))
                        .OrderByDescending(role => role.Priority)
                        .Select(role => new MindCollectiveRoleEntry(role.ID, Loc.GetString(role.Name),
                            collective.Roles.TryGetValue(m, out var roles) && roles.ContainsKey(role.ID))).ToList(),
                    CanManageMember(mind, m, "mind-collective.roles"),
                    CanManageMember(mind, m, "mind-collective.recall"))).ToList();
            NetEntity? expel = null;
            uint request = 0;
            if (TryGetExpulsion(collectiveId, mind, out var expelled, out request))
                expel = GetNetEntity(expelled);
            var menu = EnsureComp<MindCollectiveUiComponent>(actor);
            menu.State = new MindCollectiveUiState(bodies, members, canManage, HasComp<MindCollectiveObserverComponent>(actor),
                Name(collectiveId), expel, request);
            Dirty(actor, menu);
        }
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
            foreach (var (mind, observer) in collective.Minds.ToArray())
            {
                if (collective.PendingMinds.Contains(mind) || TerminatingOrDeleted(observer)
                    || !TryComp<MindCollectiveObserverComponent>(observer, out var view))
                    continue;
                if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity == null)
                    ReturnToMindspace(id, collective, mind);
                EnsureControlAction(mind);
                UpdateObserver(observer, collective);
                if (view.OpenUiAt != TimeSpan.Zero && _timing.CurTime >= view.OpenUiAt
                    && IsAccepted(observer, collective))
                {
                    view.OpenUiAt = TimeSpan.Zero;
                    _ui.OpenUi(observer, MindCollectiveUiKey.Key, observer);
                }
            }
            UpdateUi(id, collective);
        }
    }

    [SubscribeLocalEvent]
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

    [SubscribeLocalEvent]
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
        var vacant = collective.VacantObservers.Remove(ent);
        var entry = collective.Minds.FirstOrDefault(pair => pair.Value == ent.Owner);
        if (!entry.Key.IsValid())
        {
            if (vacant && !TerminatingOrDeleted(ent))
                QueueDel(ent);
            return;
        }
        var mind = entry.Key;
        if (collective.PendingMinds.Contains(mind) || TerminatingOrDeleted(mind))
        {
            RemoveMember(ent.Comp.Collective, mind, createVacancy: false);
            return;
        }
        collective.Minds.Remove(mind);
        var replacement = CreateObserver(ent.Comp.Collective, collective, mind, collective.ActiveBody);
        RefreshMemberPermissions(ent.Comp.Collective, collective, mind);
        if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity == ent.Owner)
        {
            _mind.TransferTo(mind, replacement, ghostCheckOverride: true, createGhost: false);
            EnsureControlAction(mind);
            Comp<MindCollectiveObserverComponent>(replacement).OpenUiAt = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        }
        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    public void Disband(EntityUid collectiveId)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing)
            return;
        collective.Closing = true;
        CloseConsents(collectiveId);
        foreach (var mind in collective.Minds.Keys.ToArray())
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
            {
                _ui.CloseUi(body, MindCollectiveUiKey.Key);
                RemComp<MindCollectiveUiComponent>(body);
                RemComp<MindCollectiveBodyComponent>(body);
            }
        }
        collective.Bodies.Clear();
        if (!TerminatingOrDeleted(collectiveId))
            QueueDel(collectiveId);
    }
}
