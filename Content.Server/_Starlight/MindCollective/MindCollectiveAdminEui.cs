using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Mind;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MindCollective;

[UsedImplicitly]
public sealed class MindCollectiveAdminEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IAdminLogManager _logs = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly MindCollectiveSystem _collectives;
    private readonly SharedMindSystem _minds;
    private EntityUid? _selectedCollective;
    private EntityUid? _body;
    private NetUserId? _player;
    private string _feedback = string.Empty;

    public MindCollectiveAdminEui(MindCollectiveSystem collectives, EntityUid? target)
    {
        IoCManager.InjectDependencies(this);
        _collectives = collectives;
        _minds = _entities.System<SharedMindSystem>();
        _body = target;
    }

    public override void Opened()
    {
        base.Opened();
        _player = Player.UserId;
        if (_body is { } target && _minds.TryGetMind(target, out var mind, out var consciousness))
        {
            _player = consciousness.UserId ?? _player;
            if (_collectives.TryGetCollective(mind, out var group, out _))
                _selectedCollective = group;
        }
        if (_selectedCollective == null && _body is { } body
            && _collectives.TryGetCollective(body, out var collective, out _))
            _selectedCollective = collective;
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        _admins.OnPermsChanged -= OnPermsChanged;
        base.Closed();
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Debug))
            Close();
    }

    public override EuiStateBase GetNewState()
    {
        var groups = new List<MindCollectiveAdminEntity>();
        var query = _entities.EntityQueryEnumerator<MindCollectiveComponent>();
        while (query.MoveNext(out var id, out var collective))
        {
            if (!collective.Closing && Exists(id))
                groups.Add(Entry(id));
        }
        groups = groups.OrderBy(entry => entry.Name).ToList();
        if (_selectedCollective is not { } selected || !Exists(selected)
            || !groups.Any(entry => entry.Entity == _entities.GetNetEntity(selected)))
            _selectedCollective = groups.Count > 0 ? _entities.GetEntity(groups[0].Entity) : null;
        var players = _players.Sessions.Where(session => session.Status == SessionStatus.InGame)
            .OrderBy(session => session.Name).Select(session => new MindCollectiveAdminPlayer(session.UserId, session.Name)).ToList();
        var bodies = new List<MindCollectiveAdminEntity>();
        if (_selectedCollective is { } current && _entities.TryGetComponent(current, out MindCollectiveComponent? group))
        {
            bodies.AddRange(group.Bodies.Where(Exists).Select(Entry).OrderBy(entry => entry.Name));
        }
        if (_body is { } target && Exists(target))
        {
            if (!bodies.Any(entry => entry.Entity == _entities.GetNetEntity(target)))
                bodies.Add(Entry(target));
        }
        else
            _body = bodies.Count > 0 ? _entities.GetEntity(bodies[0].Entity) : null;

        _minds.TryGetMind(_player ?? Player.UserId, out var mind, out _);
        var roles = _prototypes.EnumeratePrototypes<MindCollectiveRolePrototype>().OrderByDescending(role => role.Priority)
            .Select(role => new MindCollectiveAdminRole(role.ID, Loc.GetString(role.Name),
                _selectedCollective is { } roleGroup && _entities.TryGetComponent(roleGroup, out MindCollectiveComponent? roleCollective)
                && mind is { } member && roleCollective.Members.TryGetValue(member, out var assigned) && assigned.Roles.ContainsKey(role.ID))).ToList();
        return new MindCollectiveAdminState(groups, players, bodies, roles,
            _entities.GetNetEntity(_selectedCollective), _player, _entities.GetNetEntity(_body), _feedback,
            _selectedCollective is { } accessGroup && mind is { } accessMind && _body is { } accessBody
                ? _collectives.GetControlAccessState(accessGroup, accessMind, accessBody) : null);
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        if (!_admins.HasAdminFlag(Player, AdminFlags.Debug))
        {
            Close();
            return;
        }
        if (message is not MindCollectiveAdminMessage request)
        {
            base.HandleMessage(message);
            return;
        }
        _selectedCollective = Resolve(request.Collective);
        _body = Resolve(request.Body);
        _player = request.Player;
        if (request.Action == MindCollectiveAdminAction.Refresh)
        {
            StateDirty();
            return;
        }
        var success = Execute(request);
        _feedback = Loc.GetString(success
            ? request.Action == MindCollectiveAdminAction.Invite ? "mind-collective-admin-invited" : "mind-collective-admin-success"
            : "mind-collective-admin-failed");
        if (success)
            _logs.Add(LogType.AdminCommands, LogImpact.High,
                $"Collective debug {request.Action} by {Player.Name} ({Player.UserId}): collective {_selectedCollective}, player {_player}, body {_body}, role {request.Role}");
        StateDirty();
    }

    private bool Execute(MindCollectiveAdminMessage request)
    {
        EntityUid? mind = null;
        EntityUid? actor = null;
        if (_player is { } user && _players.TryGetSessionById(user, out var session) && session.Status == SessionStatus.InGame)
        {
            actor = session.AttachedEntity;
            _minds.TryGetMind(user, out mind, out _);
        }
        if (request.Action == MindCollectiveAdminAction.Create)
        {
            if (mind is not { } owner || !_entities.TryGetComponent(owner, out MindComponent? consciousness)
                || consciousness.OwnedEntity is not { } owned)
                return false;
            _selectedCollective = _collectives.CreateCollective(owner, owned);
            return _selectedCollective != null;
        }
        if (_selectedCollective is not { } group
            || !_entities.TryGetComponent(group, out MindCollectiveComponent? collective) || collective.Closing)
            return false;
        switch (request.Action)
        {
            case MindCollectiveAdminAction.SaveAccess:
            case MindCollectiveAdminAction.ResetAccess:
                return mind is { } configured && _body is { } configuredBody
                       && (request.Action == MindCollectiveAdminAction.ResetAccess || request.Access != null)
                       && _collectives.SetControlAccess(group, configured, configuredBody,
                           request.Action == MindCollectiveAdminAction.ResetAccess ? null : request.Access);
            case MindCollectiveAdminAction.Invite:
                return mind is { } invited && _collectives.RequestJoin(group, invited);
            case MindCollectiveAdminAction.AddBody:
                return _body is { } added && _collectives.RegisterBody(group, added);
            case MindCollectiveAdminAction.RemoveBody:
                if (_body is not { } removed || !collective.Bodies.Contains(removed))
                    return false;
                _collectives.UnregisterBody(group, removed);
                return true;
            case MindCollectiveAdminAction.SpawnBody:
                if (actor is not { } location || !_entities.TryGetComponent(location, out TransformComponent? transform)
                    || transform.MapID == MapId.Nullspace)
                    return false;
                var coordinates = _entities.System<SharedTransformSystem>().GetMoverCoordinates(location, transform);
                _body = _entities.SpawnEntity("MobHuman", coordinates.Offset(new Vector2(1, 0)));
                if (_collectives.RegisterBody(group, _body.Value))
                    return true;
                _entities.QueueDeleteEntity(_body);
                _body = null;
                return false;
            case MindCollectiveAdminAction.GrantRole:
                return mind is { } granted && request.Role is { } grantRole && _collectives.AssignRole(group, granted, grantRole);
            case MindCollectiveAdminAction.RevokeRole:
                return mind is { } revoked && request.Role is { } revokeRole && _collectives.RevokeRole(group, revoked, revokeRole);
            case MindCollectiveAdminAction.Enter:
            case MindCollectiveAdminAction.Observe:
            case MindCollectiveAdminAction.Control:
            case MindCollectiveAdminAction.Leave:
                if (actor is not { } participant || mind is not { } member
                    || !_collectives.TryGetCollective(member, out var actual, out _) || actual != group)
                    return false;
                return request.Action switch
                {
                    MindCollectiveAdminAction.Enter => _body is { } entered && _collectives.TryEnterBody(participant, entered),
                    MindCollectiveAdminAction.Observe => _body is { } viewed && _collectives.Observe(participant, viewed),
                    MindCollectiveAdminAction.Control => _body is { } controlled && _collectives.TryControlBody(participant, controlled),
                    _ => _collectives.TryLeaveBody(participant),
                };
            default:
                return false;
        }
    }

    private bool Exists(EntityUid entity)
        => _entities.TryGetComponent(entity, out MetaDataComponent? metadata) && metadata.EntityLifeStage < EntityLifeStage.Terminating;

    private EntityUid? Resolve(NetEntity? entity)
        => entity is { } net && _entities.TryGetEntity(net, out var id) && Exists(id.Value) ? id : null;

    private MindCollectiveAdminEntity Entry(EntityUid entity)
        => new(_entities.GetNetEntity(entity), $"{_entities.GetComponent<MetaDataComponent>(entity).EntityName} ({_entities.GetNetEntity(entity)})");
}
