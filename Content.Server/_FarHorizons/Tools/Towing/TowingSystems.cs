using Content.Shared._FarHorizons.Towing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.Physics;
using Content.Shared.DoAfter;
using Robust.Shared.Physics;
using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared.Coordinates;
using Content.Shared.Hands.Components;

namespace Content.Server._FarHorizons.Towing;
public sealed partial class TowingSystem : EntitySystem
{    
    [Dependency] private readonly JointSystem _joint = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
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
        
        if(ent.Comp.EntityA == null)
        {
            var jointComp = EnsureComp<JointComponent>(target);
            var visualComp = EnsureComp<JointVisualsComponent>(target);
            var tiedComp = EnsureComp<TiedComponent>(target);
            var joint = _joint.CreateDistanceJoint(used, target);
            joint.MaxLength = joint.Length + 1.0f;
            joint.Stiffness = 1f;
            joint.MinLength = 0.35f;
                        
            visualComp.Sprite = ent.Comp.RopeSprite;
            visualComp.Target = used;

            tiedComp.TiedBy = ent.Owner;
            tiedComp.isHitch = false;

            ent.Comp.EntityA = target;

            Dirty(ent.Owner, ent.Comp);
            Dirty(target, tiedComp);
            Dirty(target, visualComp);
            Dirty(target, jointComp);
        }
        else
        {
            var entA = ent.Comp.EntityA!.Value;
            _joint.ClearJoints(entA);
            var visualComp = Comp<JointVisualsComponent>(entA);
            var tiedComp = EnsureComp<TiedComponent>(target);
            var joint = _joint.CreateDistanceJoint(entA, target, anchorA: new Vector2(0f, 0.5f), anchorB: new Vector2(0f, 0.5F));
            joint.MaxLength = joint.Length + 0.2f;
            joint.Stiffness = 1f;
            joint.MinLength = 0.35f;
            
            tiedComp.AttachedTo = entA;

            visualComp.Target = target;

            if(TryComp<TiedComponent>(entA, out var tied))
            {
                tied.AttachedTo = target;
                tiedComp.isHitch = tied.isHitch;
                Dirty(entA, tied);
            }
            
            Dirty(target, tiedComp);
            Dirty(target, visualComp);
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
        var jointComp = EnsureComp<JointComponent>(target);
        var visualComp = EnsureComp<JointVisualsComponent>(target);
        var tiedComp = EnsureComp<TiedComponent>(target);
        var joint = _joint.CreateDistanceJoint(hook, target);
        joint.MaxLength = joint.Length + 1.0f;
        joint.Stiffness = 1f;
        joint.MinLength = 0.35f;
                        
        visualComp.Sprite = towComp.RopeSprite;
        visualComp.Target = hook;

        tiedComp.TiedBy = hook;
        tiedComp.isHitch = true;

        towComp.EntityA = target;

        Dirty(ent.Owner, towComp);
        Dirty(target, tiedComp);
        Dirty(target, visualComp);
        Dirty(target, jointComp);
    }
}