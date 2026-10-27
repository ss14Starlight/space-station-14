using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Hands.Components;
using Content.Shared.Mind;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    public BodyControlAccessState? GetControlAccessState(EntityUid id, EntityUid mind, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(id, out var collective) || collective.Closing
            || !collective.Members.TryGetValue(mind, out var member) || member.Pending || member.Leaving
            || !IsRegisteredBody(id, collective, body))
            return null;
        return new(GetMemberControlAccess(collective, mind, body),
            TryComp<HandsComponent>(body, out var hands) ? hands.SortedHands.ToList() : new());
    }

    private BodyControlAccess GetMemberControlAccess(MindCollectiveComponent collective, EntityUid mind, EntityUid body)
    {
        if (collective.Members.TryGetValue(mind, out var member) && member.ControlAccess.TryGetValue(body, out var access))
            return access.Copy();
        if (TryGetControlAccess(mind, body, out var granted))
            return granted;
        return new BodyControlAccess { FullControl = TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity == body };
    }

    /// <summary>Trusted configuration API. A null grant restores the role's default access.</summary>
    public bool SetControlAccess(EntityUid id, EntityUid mind, EntityUid body, BodyControlAccess? access)
    {
        if (GetControlAccessState(id, mind, body) == null || !TryComp<MindCollectiveComponent>(id, out var collective))
            return false;
        var member = collective.Members[mind];
        if (access != null)
            member.ControlAccess[body] = access.Copy();
        else
            member.ControlAccess.Remove(body);
        RefreshMemberPermissions(id, collective, mind);
        return true;
    }

    private void RefreshNativeControl(EntityUid id, MindCollectiveComponent collective, EntityUid mind)
    {
        if (!TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity is not { } body
            || !HasComp<ActorComponent>(body) || !IsRegisteredBody(id, collective, body))
            return;
        var access = GetMemberControlAccess(collective, mind, body);
        if (_bodyControl.IsControlling(body, body))
            _bodyControl.UpdateAccess(body, access);
        else if (!access.FullControl)
            _bodyControl.TryStartControl(body, body, access);
    }

    [SubscribeLocalEvent(after: [typeof(BodyControl.BodyControlSystem)])]
    private void OnCollectiveBodyAttached(Entity<MindCollectiveBodyComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!TryComp<MindCollectiveComponent>(ent.Comp.Collective, out var collective) || collective.Closing)
            return;
        if (_mind.TryGetMind(ent, out var mind, out _) && collective.Members.TryGetValue(mind, out var member)
            && !member.Pending && !member.Leaving)
            RefreshNativeControl(ent.Comp.Collective, collective, mind);
        UpdateUi(ent.Comp.Collective, collective);
    }

    private bool CanConfigureControl(EntityUid actor, EntityUid target)
        => HasPermission(actor, "mind-collective.roles")
           && (actor == target || CanManageMember(actor, target, "mind-collective.roles"));
}
