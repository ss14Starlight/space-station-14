using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;
using Content.Shared.Hands;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    private void OpenMenu(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (!collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.UserId is not { } user
            || !_players.TryGetSessionById(user, out var session) || session.Status != SessionStatus.InGame
            || session.AttachedEntity is not { } actor || !TryGetActorMind(actor, id, collective, out var actual) || actual != mind)
            return;
        if (member.Menu is { IsClosed: false } current)
        {
            current.StateDirty();
            return;
        }
        member.Menu = new MindCollectiveEui(this, id, mind);
        _eui.OpenEui(member.Menu, session);
    }

    public void MenuClosed(MindCollectiveEui menu)
    {
        if (TryComp<MindCollectiveComponent>(menu.Collective, out var collective)
            && collective.Members.TryGetValue(menu.Mind, out var member) && member.Menu == menu)
            member.Menu = null;
    }

    private void UpdateUi(EntityUid id, MindCollectiveComponent collective)
    {
        foreach (var member in collective.Members.Values)
        {
            if (member.Menu is { IsClosed: false } menu)
                menu.StateDirty();
        }
    }

    private bool TryGetMenuActor(MindCollectiveEui menu, out EntityUid actor, out MindCollectiveComponent collective)
    {
        actor = default;
        collective = default!;
        if (menu.Player.Status != SessionStatus.InGame || menu.Player.AttachedEntity is not { } attached
            || !TryGetActorCollective(attached, out var id, out collective, out var mind)
            || id != menu.Collective || mind != menu.Mind || collective.Members[mind].Menu != menu)
            return false;
        actor = attached;
        return true;
    }

    public MindCollectiveUiState GetMenuState(MindCollectiveEui menu)
    {
        if (!TryGetMenuActor(menu, out var actor, out var collective))
            return new([], [], false, false, string.Empty);
        var id = menu.Collective;
        var mind = menu.Mind;
        var bodies = new List<MindCollectiveBodyEntry>();
        foreach (var body in collective.Bodies)
        {
            if (!IsRegisteredBody(id, collective, body))
                continue;
            var occupied = _mind.TryGetMind(body, out var occupant, out var occupiedMind);
            bodies.Add(new(GetNetEntity(body), Name(body), occupied ? occupiedMind!.CharacterName ?? Name(body) : null,
                CanEnter(id, collective, mind, body), occupied && CanManageMember(mind, occupant, "mind-collective.recall"),
                occupied ? GetNetEntity(occupant) : null,
                CanControlBody(id, collective, mind, actor, body), _bodyControl.IsControlling(actor, body)));
        }
        var canManageRoles = HasPermission(mind, "mind-collective.roles");
        var priority = RolePriority(collective, mind);
        var roles = _prototypes.EnumeratePrototypes<MindCollectiveRolePrototype>()
            .OrderByDescending(role => role.Priority).ToList();
        var members = collective.Members.Where(pair => !pair.Value.Pending && !pair.Value.Leaving)
            .Select(pair => new MindCollectiveMemberEntry(GetNetEntity(pair.Key),
                TryComp<MindComponent>(pair.Key, out var member) ? member.CharacterName ?? Name(pair.Key) : Name(pair.Key),
                CanManageMember(mind, pair.Key, "mind-collective.expel"),
                roles.Where(role => pair.Value.Roles.ContainsKey(role.ID) || canManageRoles && role.Priority <= priority)
                    .Select(role => new MindCollectiveRoleEntry(role.ID, Loc.GetString(role.Name), pair.Value.Roles.ContainsKey(role.ID))).ToList(),
                CanManageMember(mind, pair.Key, "mind-collective.roles"),
                CanManageMember(mind, pair.Key, "mind-collective.recall"), CanConfigureControl(mind, pair.Key))).ToList();
        MindCollectiveControlEditorState? editor = null;
        if (menu.EditingMind is { } editing && menu.EditingBody is { } bodyId && CanConfigureControl(mind, editing)
            && GetControlAccessState(id, editing, bodyId) is { } settings)
            editor = new(GetNetEntity(editing), GetNetEntity(bodyId),
                TryComp<MindComponent>(editing, out var edited) ? edited.CharacterName ?? Name(editing) : Name(editing), settings);
        else
        {
            menu.EditingMind = null;
            menu.EditingBody = null;
        }
        return new(bodies, members, canManageRoles || HasPermission(mind, "mind-collective.expel"),
            HasComp<MindCollectiveObserverComponent>(actor), Name(id), editor);
    }

    public bool HandleMenuMessage(MindCollectiveEui menu, EuiMessageBase message)
    {
        if (!TryGetMenuActor(menu, out var actor, out var collective))
            return false;
        switch (message)
        {
            case EnterCollectiveBodyMessage enter when TryGetEntity(enter.Body, out var entered):
                TryEnterBody(actor, entered.Value);
                break;
            case ObserveCollectiveBodyMessage observe when TryGetEntity(observe.Body, out var viewed):
                Observe(actor, viewed.Value);
                break;
            case ControlCollectiveBodyMessage control when TryGetEntity(control.Body, out var controlled):
                TryControlBody(actor, controlled.Value);
                break;
            case LeaveCollectiveBodyMessage:
                TryLeaveBody(actor);
                break;
            case RecallCollectiveMemberMessage recall when TryGetEntity(recall.Mind, out var recalled):
                TryRecallMember(actor, recalled.Value);
                break;
            case ExpelCollectiveMemberMessage expel when TryGetEntity(expel.Mind, out var expelled):
                Expel(actor, expelled.Value);
                break;
            case SetCollectiveRoleMessage role when TryGetEntity(role.Mind, out var target)
                && CanManageMember(menu.Mind, target.Value, "mind-collective.roles")
                && _prototypes.TryIndex(role.Role, out var prototype) && prototype.Priority <= RolePriority(collective, menu.Mind):
                if (role.Assigned)
                    AssignRole(menu.Collective, target.Value, role.Role);
                else
                    RevokeRole(menu.Collective, target.Value, role.Role);
                break;
            case ConfigureCollectiveControlMessage configure:
                if (configure.Mind is not { } netMind)
                {
                    menu.EditingMind = null;
                    menu.EditingBody = null;
                }
                else if (TryGetEntity(netMind, out var editing) && CanConfigureControl(menu.Mind, editing.Value))
                {
                    menu.EditingMind = editing;
                    menu.EditingBody = configure.Body is { } netBody && TryGetEntity(netBody, out var body)
                        && IsRegisteredBody(menu.Collective, collective, body.Value) ? body
                        : collective.Bodies.Count > 0 ? collective.Bodies.First() : null;
                }
                break;
            case SetCollectiveControlMessage setControl when TryGetEntity(setControl.Mind, out var configured)
                && TryGetEntity(setControl.Body, out var configuredBody) && CanConfigureControl(menu.Mind, configured.Value):
                SetControlAccess(menu.Collective, configured.Value, configuredBody.Value, setControl.Access);
                break;
        }
        if (!menu.IsClosed)
            menu.StateDirty();
        return true;
    }

    [SubscribeLocalEvent]
    private void OnCollectiveEntityRenamed(ref EntityRenamedEvent args)
    {
        if (TryGetCollective(args.Uid, out var id, out var collective))
            UpdateUi(id, collective);
    }

    [SubscribeLocalEvent]
    private void OnControlSourceStarted(Entity<BodyControlSourceComponent> ent, ref ComponentStartup args)
        => UpdateEntityMenus(ent);

    [SubscribeLocalEvent]
    private void OnControlSourceStopped(Entity<BodyControlSourceComponent> ent, ref ComponentShutdown args)
        => UpdateEntityMenus(ent);

    [SubscribeLocalEvent]
    private void OnPoolMindAdded(Entity<MindCollectiveBodyComponent> ent, ref MindAddedMessage args)
        => UpdateEntityMenus(ent);

    [SubscribeLocalEvent]
    private void OnPoolMindRemoved(Entity<MindCollectiveBodyComponent> ent, ref MindRemovedMessage args)
        => UpdateEntityMenus(ent);

    [SubscribeLocalEvent]
    private void OnPoolPlayerDetached(Entity<MindCollectiveBodyComponent> ent, ref PlayerDetachedEvent args)
        => UpdateEntityMenus(ent);

    [SubscribeLocalEvent]
    private void OnPoolHandsChanged(Entity<MindCollectiveBodyComponent> ent, ref HandCountChangedEvent args)
        => UpdateEntityMenus(ent);

    private void UpdateEntityMenus(EntityUid entity)
    {
        if (TryGetCollective(entity, out var id, out var collective))
            UpdateUi(id, collective);
    }
}
