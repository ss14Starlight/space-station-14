using Content.Shared._FarHorizons.Towing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.Physics;
using Content.Shared.DoAfter;
using Robust.Shared.Physics;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared.Coordinates;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Server._FarHorizons.Towing;
public sealed partial class TowingSystem : EntitySystem
{    
    [Dependency] private readonly JointSystem _joint = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    private static readonly string _towingRope = "TowingRope";
    private static readonly string _hitchHook = "HitchHook";
    public override void Initialize()
    {
        SubscribeLocalEvent<TowingComponent, GetVerbsEvent<UtilityVerb>>(OnAddUtilityVerb);
        SubscribeLocalEvent<TiedComponent, GetVerbsEvent<AlternativeVerb>>(OnAddUnTieVerb);
        SubscribeLocalEvent<HitchComponent, GetVerbsEvent<AlternativeVerb>>(OnAddHitchVerb);
        SubscribeLocalEvent<TowingComponent, TieUpDoAfter>(OnTieUpDoAfter);
        SubscribeLocalEvent<TiedComponent, UnTieDoAfter>(OnUnTieDoAfter);
        SubscribeLocalEvent<HandsComponent, DeployHitchDoAfter>(OnDeployHitchDoAfter);
        SubscribeLocalEvent<TiedComponent, JointRemovedEvent>(OnJointRemoved);
        base.Initialize();
    }

    public void OnAddUtilityVerb(EntityUid ent, TowingComponent component, GetVerbsEvent<UtilityVerb> args)
    {
        if(!args.CanAccess || !args.CanInteract || args.Hands == null) return;
        if(HasComp<TiedComponent>(args.Target)) return;
        if(TryComp<PhysicsComponent>(args.Target, out var physicsComponent) && physicsComponent.BodyType == BodyType.Static) return;
        var tieVerb = new UtilityVerb
        {
            Text = Loc.GetString("towing-rope-tie"),
            Act = () =>
            {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.TieUpTime, new TieUpDoAfter(), ent, target: args.Target, used: ent)
                    {
                        BreakOnMove = true,
                    };
                    
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        };
        args.Verbs.Add(tieVerb);
    }

    public void OnTieUpDoAfter(Entity<TowingComponent> ent, ref TieUpDoAfter args)
    {
        if(args.Cancelled) return;
        if(args.Target == null || args.Used == null) return;
        var target = args.Target.Value;
        var used = args.Used.Value;
        args.Handled = true;
        
        if(ent.Comp.EntityA == null || !EntityManager.EntityExists(ent.Comp.EntityA))
        {
            CreateJoint(used, target, ent.Comp, used);
        }
        else
        {
            var entA = ent.Comp.EntityA.Value;
            _joint.ClearJoints(entA);

            if(TryComp<TiedComponent>(entA, out var tied))
                CreateJoint(entA, target, ent.Comp, isHitch:tied.isHitch);
            else
                CreateJoint(entA, target, ent.Comp);
            QueueDel(ent.Owner);
        }
    }

    public void OnAddUnTieVerb(EntityUid ent, TiedComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if(!args.CanAccess || !args.CanInteract || args.Hands == null) return;
        if(!HasComp<TiedComponent>(args.Target)) return;
        var untieRopeVerb = new AlternativeVerb
        {
            Text = Loc.GetString("towing-rope-untie"),
            Act = () =>
            {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.UntieTime, new UnTieDoAfter(), args.Target, target: args.Target)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        BreakOnDropItem = true,
                        BreakOnHandChange = true
                    };
                        
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        };
        args.Verbs.Add(untieRopeVerb);
    }

    public void OnUnTieDoAfter(Entity<TiedComponent> ent, ref UnTieDoAfter args)
    {
        if(args.Cancelled) return;
        if(args.Target == null) return;
        var target = args.Target.Value;
        if(ent.Comp.TiedBy != null)
        {
            if(TryComp<TowingComponent>(ent.Comp.TiedBy.Value, out var tow) && !ent.Comp.isHitch)
            {
                tow.EntityA = null;
                Dirty(ent.Comp.TiedBy.Value, tow);
            }
            else
            {
                QueueDel(ent.Comp.TiedBy.Value);
            }
        }
        if(TryComp<TiedComponent>(target, out var tComp))
        {
            if(tComp.AttachedTo != null)
            {
                var attachedTo = tComp.AttachedTo.Value;
                _movementSpeed.RefreshMovementSpeedModifiers(attachedTo);
                RemComp<TiedComponent>(attachedTo);
                RemComp<JointComponent>(attachedTo);
                RemComp<JointVisualsComponent>(attachedTo);
                if(!tComp.isHitch)
                {
                    var newrope = SpawnAtPosition(_towingRope, target.ToCoordinates());
                    _hands.TryPickupAnyHand(args.User, newrope);   
                }             
            }
        }
        _movementSpeed.RefreshMovementSpeedModifiers(target);    
        RemComp<TiedComponent>(target);
        RemComp<JointComponent>(target);
        RemComp<JointVisualsComponent>(target);
        _joint.RecursiveClearJoints(target);
    }

    public void OnAddHitchVerb(EntityUid uid, HitchComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if(!args.CanAccess || !args.CanInteract || args.Hands == null) return;
        if(HasComp<TiedComponent>(args.Target)) return;
        if(TryComp<TowingComponent>(args.Target, out var TowComp) && TowComp.EntityA != null) return;
        var untieRopeVerb = new AlternativeVerb
        {
            Text = Loc.GetString("towing-hitch-deploy"),
            Act = () =>
            {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.HitchDeploy, new DeployHitchDoAfter(), args.User, target: args.Target)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        BreakOnDropItem = true,
                        BreakOnHandChange = true
                    };
                        
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        };
        args.Verbs.Add(untieRopeVerb);
    }

    public void OnDeployHitchDoAfter(Entity<HandsComponent> ent, ref DeployHitchDoAfter args)
    {
        if(args.Target == null || args.Cancelled) return;
        var target = args.Target.Value;

        var hook = SpawnAtPosition(_hitchHook, target.ToCoordinates());
        _hands.TryPickupAnyHand(args.User, hook);
        
        var towComp = Comp<TowingComponent>(hook);
        CreateJoint(hook, target, towComp, hook, true);
    }

    private void CreateJoint(EntityUid entityA, EntityUid entityB, TowingComponent towComp, EntityUid? TiedBy=null, bool isHitch=false)
    {
        var jointComp = EnsureComp<JointComponent>(entityB);
        var visualComp = EnsureComp<JointVisualsComponent>(entityB);
        var tiedComp = EnsureComp<TiedComponent>(entityB);
        var joint = _joint.CreateDistanceJoint(entityA, entityB);
        joint.MaxLength = joint.Length + 0.3f;
        joint.Stiffness = 1f;
        joint.MinLength = 0.0f;
                        
        visualComp.Sprite = towComp.RopeSprite;
        visualComp.Target = entityA;

        tiedComp.TiedBy = TiedBy;
        tiedComp.isHitch = isHitch;
        tiedComp.AttachedTo = entityA;

        if (TryComp<TiedComponent>(entityA, out var tied))
        {
            tied.AttachedTo = entityB;
            tied.TiedBy = null;
            Dirty(entityA, tied);
        }

        towComp.EntityA = entityB;
        
        _movementSpeed.RefreshMovementSpeedModifiers(entityA);
        _movementSpeed.RefreshMovementSpeedModifiers(entityB);

        if(TiedBy != null)
            Dirty(TiedBy.Value, towComp);
        Dirty(entityB, tiedComp);
        Dirty(entityB, visualComp);
        Dirty(entityB, jointComp);
    }

    private void OnJointRemoved(Entity<TiedComponent> ent, ref JointRemovedEvent args)
    {
        if(!HasComp<TiedComponent>(args.OurEntity) || !HasComp<TiedComponent>(args.OtherEntity)) return;
        if(ent.Comp.AttachedTo != null)
        {
            RemComp<JointVisualsComponent>(ent.Comp.AttachedTo.Value);
            RemComp<TiedComponent>(ent.Comp.AttachedTo.Value);
        }
        RemComp<JointVisualsComponent>(ent.Owner);
        RemComp<TiedComponent>(ent.Owner);
    }
}