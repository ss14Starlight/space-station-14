using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Mind;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private IPrototypeManager _rolePrototypes = default!;
    [Dependency] private BodyControl.BodyControlSystem _bodyControl = default!;

    public bool HasPermission(EntityUid mind, string permission, EntityUid? body = null)
    {
        if (!TryGetCollective(mind, out _, out var collective) || collective.PendingMinds.Contains(mind)
            || !collective.Roles.TryGetValue(mind, out var roles))
            return false;
        return roles.Any(pair => (body == null || pair.Value.Bodies == null || pair.Value.Bodies.Contains(body.Value))
            && _rolePrototypes.TryIndex(pair.Key, out var role)
            && (role.Permissions.Contains(permission) || role.Permissions.Contains("*")));
    }

    private int RolePriority(MindCollectiveComponent collective, EntityUid mind)
        => collective.Roles.TryGetValue(mind, out var roles)
            ? roles.Keys.Select(id => _rolePrototypes.Index(id).Priority).DefaultIfEmpty(int.MinValue).Max()
            : int.MinValue;

    public bool CanManageMember(EntityUid mind, EntityUid target, string permission)
        => mind != target && TryGetCollective(mind, out var id, out var collective)
           && collective.Minds.ContainsKey(target) && !collective.PendingMinds.Contains(target)
           && TryComp<MindCollectiveMemberComponent>(target, out var member) && member.Collective == id
           && HasPermission(mind, permission) && RolePriority(collective, mind) > RolePriority(collective, target);

    /// <summary>Trusted API. Unique roles are transferred, without changing the group's identity or lifetime.</summary>
    public bool AssignRole(EntityUid collectiveId, EntityUid mind, ProtoId<MindCollectiveRolePrototype> roleId,
        HashSet<EntityUid>? bodies = null, BodyControlAccess? control = null)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !collective.Minds.ContainsKey(mind) || collective.PendingMinds.Contains(mind)
            || !_rolePrototypes.TryIndex(roleId, out var role))
            return false;
        if (role.Unique)
        {
            foreach (var (other, assignments) in collective.Roles.ToArray())
            {
                if (other != mind && assignments.Remove(roleId))
                    RefreshMemberPermissions(collectiveId, collective, other);
            }
        }
        if (!collective.Roles.TryGetValue(mind, out var roles))
            collective.Roles[mind] = roles = new();
        roles[roleId] = new MindCollectiveRoleAssignment
        {
            Bodies = bodies == null ? null : new(bodies),
            Control = control?.Copy(),
        };
        RefreshMemberPermissions(collectiveId, collective, mind);
        return true;
    }

    public bool RevokeRole(EntityUid collectiveId, EntityUid mind, ProtoId<MindCollectiveRolePrototype> role)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !collective.Roles.TryGetValue(mind, out var roles) || !roles.Remove(role))
            return false;
        RefreshMemberPermissions(collectiveId, collective, mind);
        return true;
    }

    private void EnsureDefaultRole(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (!collective.Roles.ContainsKey(mind))
            AssignRole(id, mind, collective.DefaultRole);
    }

    private void RemoveMemberRoles(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Minds.TryGetValue(mind, out var observer))
            _bodyControl.StopControl(observer);
        if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity is { } actor)
            _bodyControl.StopControl(actor);
        collective.Roles.Remove(mind);
    }

    private void RefreshMemberPermissions(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Minds.TryGetValue(mind, out var observer)
            && TryComp<MindCollectiveObserverComponent>(observer, out var view))
        {
            RefreshObserverWorldPermissions(observer, view, mind);
            if (TryComp<BodyControlSourceComponent>(observer, out var source))
            {
                if (HasPermission(mind, "body.observe", source.Body) && TryGetControlAccess(mind, source.Body, out var access))
                    _bodyControl.UpdateAccess(observer, access);
                else
                    _bodyControl.StopControl(observer);
            }
        }
        UpdateUi(id, collective);
    }

    private void RefreshObserverWorldPermissions(EntityUid observer, MindCollectiveObserverComponent view, EntityUid mind)
    {
        var body = view.Viewing;
        var interact = body != null && HasPermission(mind, "world.interact", body);
        var conscious = body != null && HasPermission(mind, "world.conscious", body);
        var speak = body != null && HasPermission(mind, "world.speak", body);
        var point = body != null && HasPermission(mind, "world.point", body);
        if (view.AllowWorldInteraction == interact && view.AllowConsciousActions == conscious
            && view.AllowWorldSpeech == speak && view.AllowWorldPointing == point)
            return;
        view.AllowWorldInteraction = interact;
        view.AllowConsciousActions = conscious;
        view.AllowWorldSpeech = speak;
        view.AllowWorldPointing = point;
        Dirty(observer, view);
        _collectiveLanguage.UpdateEntityLanguages(observer);
    }

    // Select one complete grant, rather than combining permissions from one role with another role's hand scope.
    private bool TryGetControlAccess(EntityUid mind, EntityUid body, out BodyControlAccess access)
    {
        access = default!;
        if (!TryGetCollective(mind, out _, out var collective) || collective.PendingMinds.Contains(mind)
            || !collective.Roles.TryGetValue(mind, out var roles))
            return false;
        foreach (var (roleId, assignment) in roles.OrderByDescending(pair => _rolePrototypes.Index(pair.Key).Priority))
        {
            var role = _rolePrototypes.Index(roleId);
            if (assignment.Bodies != null && !assignment.Bodies.Contains(body)
                || !role.Permissions.Contains("body.control") && !role.Permissions.Contains("*"))
                continue;
            access = (assignment.Control ?? role.Control).Copy();
            return true;
        }
        return false;
    }

    [SubscribeLocalEvent]
    private void OnSetRoleMessage(Entity<UserInterfaceComponent> ent, ref SetCollectiveRoleMessage args)
    {
        var target = GetEntity(args.Mind);
        if (args.Actor != ent.Owner || !TryGetActorCollective(ent, out var id, out var collective, out var actor)
            || !CanManageMember(actor, target, "mind-collective.roles")
            || !_rolePrototypes.TryIndex(args.Role, out var role) || role.Priority > RolePriority(collective, actor))
            return;
        if (args.Assigned)
            AssignRole(id, target, args.Role);
        else
            RevokeRole(id, target, args.Role);
    }
}
