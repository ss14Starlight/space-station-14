using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Hands.Components;
using Content.Shared.Mind;
using Content.Shared.UserInterface;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    public BodyControlAccessState? GetControlAccessState(EntityUid id, EntityUid mind, EntityUid body)
    {
        if (!TryComp<MindCollectiveComponent>(id, out var collective) || collective.Closing
            || !collective.Minds.ContainsKey(mind) || collective.PendingMinds.Contains(mind)
            || !IsRegisteredBody(id, collective, body))
            return null;
        return new(GetMemberControlAccess(collective, mind, body),
            TryComp<HandsComponent>(body, out var hands) ? hands.SortedHands.ToList() : new());
    }

    private BodyControlAccess GetMemberControlAccess(MindCollectiveComponent collective, EntityUid mind, EntityUid body)
    {
        if (collective.ControlAccess.TryGetValue(mind, out var bodies) && bodies.TryGetValue(body, out var access))
            return access.Copy();
        if (TryGetControlAccess(mind, body, out access))
            return access;
        return new BodyControlAccess { FullControl = TryComp<MindComponent>(mind, out var consciousness) && consciousness.OwnedEntity == body };
    }

    /// <summary>Trusted configuration API. A null grant restores the role's default access.</summary>
    public bool SetControlAccess(EntityUid id, EntityUid mind, EntityUid body, BodyControlAccess? access)
    {
        if (GetControlAccessState(id, mind, body) == null || !TryComp<MindCollectiveComponent>(id, out var collective))
            return false;
        if (access != null)
        {
            if (!collective.ControlAccess.TryGetValue(mind, out var bodies))
                collective.ControlAccess[mind] = bodies = new();
            bodies[body] = access.Copy();
        }
        else if (collective.ControlAccess.TryGetValue(mind, out var bodies))
        {
            bodies.Remove(body);
            if (bodies.Count == 0)
                collective.ControlAccess.Remove(mind);
        }
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
        if (TryComp<MindCollectiveComponent>(ent.Comp.Collective, out var collective) && !collective.Closing
            && _mind.TryGetMind(ent, out var mind, out _) && collective.Minds.ContainsKey(mind)
            && !collective.PendingMinds.Contains(mind))
            RefreshNativeControl(ent.Comp.Collective, collective, mind);
    }

    private bool CanConfigureControl(EntityUid actor, EntityUid target)
        => HasPermission(actor, "mind-collective.roles")
           && (actor == target || CanManageMember(actor, target, "mind-collective.roles"));

    [SubscribeLocalEvent]
    private void OnConfigureControl(Entity<UserInterfaceComponent> ent, ref ConfigureCollectiveControlMessage args)
    {
        if (args.Actor != ent.Owner || !TryGetActorCollective(ent, out var id, out var collective, out var actor))
            return;
        var menu = EnsureComp<MindCollectiveUiComponent>(ent);
        if (args.Mind is not { } netMind)
        {
            menu.EditingMind = null;
            menu.EditingBody = null;
        }
        else if (TryGetEntity(netMind, out var target) && CanConfigureControl(actor, target.Value))
        {
            menu.EditingMind = target;
            menu.EditingBody = args.Body is { } netBody && TryGetEntity(netBody, out var body)
                && IsRegisteredBody(id, collective, body.Value) ? body
                : collective.Bodies.Count > 0 ? collective.Bodies.First() : null;
        }
        UpdateUi(id, collective);
    }

    [SubscribeLocalEvent]
    private void OnSetControlAccess(Entity<UserInterfaceComponent> ent, ref SetCollectiveControlMessage args)
    {
        if (args.Actor == ent.Owner && TryGetActorCollective(ent, out var id, out _, out var actor)
            && TryGetEntity(args.Mind, out var target) && TryGetEntity(args.Body, out var body)
            && CanConfigureControl(actor, target.Value))
            SetControlAccess(id, target.Value, body.Value, args.Access);
    }
}
