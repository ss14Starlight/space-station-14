using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private BodyControl.BodyControlSystem _bodyControl = default!;

    public bool HasPermission(EntityUid mind, string permission, EntityUid? body = null)
    {
        if (!TryGetCollective(mind, out _, out var collective)
            || !collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving)
            return false;
        return member.Roles.Any(pair => (body == null || pair.Value.Bodies == null || pair.Value.Bodies.Contains(body.Value))
            && _prototypes.TryIndex(pair.Key, out var role)
            && (role.Permissions.Contains(permission) || role.Permissions.Contains("*")));
    }

    private int RolePriority(MindCollectiveComponent collective, EntityUid mind)
        => collective.Members.TryGetValue(mind, out var member)
            ? member.Roles.Keys.Select(id => _prototypes.Index(id).Priority).DefaultIfEmpty(int.MinValue).Max()
            : int.MinValue;

    public bool CanManageMember(EntityUid mind, EntityUid target, string permission)
        => mind != target && TryGetCollective(mind, out var id, out var collective)
           && collective.Members.TryGetValue(target, out var targetMember) && !targetMember.Pending && !targetMember.Leaving
           && TryComp<MindCollectiveMemberComponent>(target, out var membership) && membership.Collective == id
           && HasPermission(mind, permission) && RolePriority(collective, mind) > RolePriority(collective, target);

    /// <summary>Trusted API. Unique roles are transferred without changing the group's identity or lifetime.</summary>
    public bool AssignRole(EntityUid collectiveId, EntityUid mind, ProtoId<MindCollectiveRolePrototype> roleId,
        HashSet<EntityUid>? bodies = null, BodyControlAccess? control = null)
    {
        if (!TryComp<MindCollectiveComponent>(collectiveId, out var collective) || collective.Closing
            || !collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving
            || !_prototypes.TryIndex(roleId, out var role))
            return false;
        if (role.Unique)
        {
            foreach (var (other, assigned) in collective.Members)
            {
                if (other != mind && assigned.Roles.Remove(roleId))
                    RefreshMemberPermissions(collectiveId, collective, other);
            }
        }
        member.Roles[roleId] = new MindCollectiveRoleAssignment
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
            || !collective.Members.TryGetValue(mind, out var member) || !member.Roles.Remove(role))
            return false;
        RefreshMemberPermissions(collectiveId, collective, mind);
        return true;
    }

    private void EnsureDefaultRole(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Members.TryGetValue(mind, out var member) && member.Roles.Count == 0)
            AssignRole(id, mind, collective.DefaultRole);
    }

    private void RemoveMemberRoles(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (collective.Members.TryGetValue(mind, out var member))
        {
            _bodyControl.StopControl(member.Observer);
            member.Roles.Clear();
        }
        if (TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity is { } actor)
            _bodyControl.StopControl(actor);
    }

    private void RefreshMemberPermissions(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        RefreshNativeControl(id, collective, mind);
        if (collective.Members.TryGetValue(mind, out var member)
            && TryComp<MindCollectiveObserverComponent>(member.Observer, out var view))
        {
            RefreshObserverWorldPermissions(member.Observer, view, mind);
            if (TryComp<BodyControlSourceComponent>(member.Observer, out var source))
            {
                if (HasPermission(mind, "body.observe", source.Body) && TryGetControlAccess(mind, source.Body, out var access))
                    _bodyControl.UpdateAccess(member.Observer, access);
                else
                    _bodyControl.StopControl(member.Observer);
            }
            UpdateObserver(member.Observer, collective);
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

    // Select one complete grant rather than mixing permissions from one role with another role's hand scope.
    private bool TryGetControlAccess(EntityUid mind, EntityUid body, out BodyControlAccess access)
    {
        access = default!;
        if (!TryGetCollective(mind, out _, out var collective)
            || !collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving)
            return false;
        foreach (var (roleId, assignment) in member.Roles.OrderByDescending(pair => _prototypes.Index(pair.Key).Priority))
        {
            var role = _prototypes.Index(roleId);
            if (assignment.Bodies != null && !assignment.Bodies.Contains(body)
                || !role.Permissions.Contains("body.control") && !role.Permissions.Contains("*"))
                continue;
            access = member.ControlAccess.TryGetValue(body, out var configured)
                ? configured.Copy() : (assignment.Control ?? role.Control).Copy();
            return true;
        }
        return false;
    }
}
