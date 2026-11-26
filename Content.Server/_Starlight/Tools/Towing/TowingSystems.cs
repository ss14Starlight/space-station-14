using Content.Shared._FarHorizons.Towing.Components;
using Content.Shared._Starlight.Tools.Towing;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Robust.Server.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Tools.Towing;

public sealed partial class TowingSystem : EntitySystem
{
    private static readonly EntProtoId HitchHook = "HitchHook";

    [Dependency] private JointSystem _joint = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private IGameTiming _timing = default!;

    private uint _nextJointId;

    public override void Initialize()
    {
        SubscribeLocalEvent<TowingRopeComponent, GetVerbsEvent<UtilityVerb>>(OnAddTieVerb);
        SubscribeLocalEvent<TiedComponent, GetVerbsEvent<AlternativeVerb>>(OnAddUntieVerb);
        SubscribeLocalEvent<HitchComponent, GetVerbsEvent<AlternativeVerb>>(OnAddHitchVerb);
        SubscribeLocalEvent<TowingRopeComponent, TieUpDoAfter>(OnTieUpDoAfter);
        SubscribeLocalEvent<TiedComponent, UnTieDoAfter>(OnUntieDoAfter);
        SubscribeLocalEvent<HandsComponent, DeployHitchDoAfter>(OnDeployHitchDoAfter);
        SubscribeLocalEvent<TiedComponent, JointRemovedEvent>(OnJointRemoved);
    }

    private void OnAddTieVerb(EntityUid uid, TowingRopeComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Target == uid ||
            (HasComp<TiedComponent>(uid) && component.FirstEnd == null) || !CanTieTo(args.Target))
            return;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("towing-rope-tie"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, args.User, component.TieUpTime, new TieUpDoAfter(), uid, target: args.Target, used: uid)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnTieUpDoAfter(Entity<TowingRopeComponent> rope, ref TieUpDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || target == rope.Owner ||
            !_hands.IsHolding(args.User, rope.Owner) || !CanTieTo(target))
            return;

        var hasCharges = TryComp<LimitedChargesComponent>(rope, out var charges);
        if (hasCharges && _charges.IsEmpty((rope.Owner, charges)))
            return;

        if (!TryComp<TiedComponent>(rope, out var preview))
        {
            LinkRope(rope, target);
            args.Handled = true;
            return;
        }

        if (rope.Comp.FirstEnd is not { } first || preview.AttachedTo != first ||
            !IsTowable(first) || Transform(first).MapID != Transform(target).MapID)
            return;

        Unlink((rope.Owner, preview));
        CreateJoint(first, target, rope.Comp);

        if (hasCharges && _charges.TryUseCharge((rope.Owner, charges)) && _charges.IsEmpty((rope.Owner, charges)))
            QueueDel(rope);

        args.Handled = true;
    }

    private void OnAddUntieVerb(EntityUid uid, TiedComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || component.Joint == null)
            return;

        var joint = component.Joint;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("towing-rope-untie"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, args.User, component.UntieTime, new UnTieDoAfter(joint.ID), uid, target: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnHandChange = true,
            }),
        });
    }

    private void OnUntieDoAfter(Entity<TiedComponent> ent, ref UnTieDoAfter args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Joint?.ID != args.JointId)
            return;

        Unlink(ent);
        args.Handled = true;
    }

    private void OnAddHitchVerb(EntityUid uid, HitchComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || !CanTieTo(uid))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("towing-hitch-deploy"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, args.User, component.HitchDeploy, new DeployHitchDoAfter(), args.User, target: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnHandChange = true,
            }),
        });
    }

    private void OnDeployHitchDoAfter(Entity<HandsComponent> ent, ref DeployHitchDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !HasComp<HitchComponent>(target) || !CanTieTo(target))
            return;

        var hook = SpawnAtPosition(HitchHook, target.ToCoordinates());
        _hands.TryPickupAnyHand(args.User, hook);
        args.Handled = true;

        // Send the hook to clients before sending a joint that refers to it.
        Timer.Spawn(_timing.TickPeriod * 2, () => FinishDeployingHitch(hook, target));
    }

    private void FinishDeployingHitch(EntityUid hook, EntityUid target)
    {
        if (TerminatingOrDeleted(hook) || !TryComp<TowingRopeComponent>(hook, out var rope) ||
            HasComp<TiedComponent>(hook))
            return;

        if (!HasComp<HitchComponent>(target) || !CanTieTo(target) ||
            Transform(hook).MapID != Transform(target).MapID)
        {
            QueueDel(hook);
            return;
        }

        LinkRope((hook, rope), target);
    }

    private bool CanTieTo(EntityUid uid) => !HasComp<TiedComponent>(uid) && IsTowable(uid);

    private bool IsTowable(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType == BodyType.Static)
            return false;

        var transform = Transform(uid);
        return transform.ParentUid == transform.GridUid;
    }

    private void LinkRope(Entity<TowingRopeComponent> rope, EntityUid first)
    {
        CreateJoint(rope.Owner, first, rope.Comp);
        rope.Comp.FirstEnd = first;
    }

    private void CreateJoint(EntityUid first, EntityUid second, TowingRopeComponent rope)
    {
        // A pending untie belongs to this link, even if the same targets are tied again.
        var joint = _joint.CreateDistanceJoint(first, second, id: $"towing-{_nextJointId++}");
        joint.MaxLength = joint.Length + 0.3f;
        joint.Stiffness = 1f;
        joint.MinLength = 0.0f;

        var visuals = EnsureComp<JointVisualsComponent>(second);
        visuals.Sprite = rope.RopeSprite;
        visuals.Target = first;
        Dirty(second, visuals);

        SetEndpoint(first, second, joint);
        SetEndpoint(second, first, joint);
    }

    private void SetEndpoint(EntityUid uid, EntityUid other, Joint joint)
    {
        var tied = EnsureComp<TiedComponent>(uid);
        tied.AttachedTo = other;
        tied.Joint = joint;
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnJointRemoved(Entity<TiedComponent> ent, ref JointRemovedEvent args)
    {
        if (ent.Comp.Joint == args.Joint)
            ClearEndpoints(ent, args.Joint);
    }

    private void Unlink(Entity<TiedComponent> ent)
    {
        if (ent.Comp.Joint is not { } joint)
            return;

        ClearEndpoints(ent, joint);
        _joint.RemoveJoint(joint);
    }

    private void ClearEndpoints(Entity<TiedComponent> ent, Joint joint)
    {
        var other = ent.Comp.AttachedTo;
        if (TryComp<TiedComponent>(other, out var tied) && tied.Joint == joint)
            ClearEndpoint((other.Value, tied));

        ClearEndpoint(ent);
    }

    private void ClearEndpoint(Entity<TiedComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<TowingRopeComponent>(ent, out var rope) && rope.FirstEnd == ent.Comp.AttachedTo)
        {
            rope.FirstEnd = null;
            if (HasComp<HitchComponent>(ent))
                QueueDel(ent);
        }

        if (TryComp<JointVisualsComponent>(ent, out var visuals) && visuals.Target == ent.Comp.AttachedTo)
            RemComp(ent, visuals);

        RemComp(ent, ent.Comp);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }
}
