using Content.Shared._Starlight.Polymorph.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Silicons.Borgs;

public sealed class StationAIShuntSystem : EntitySystem
{

    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly StationAiVisionSystem _vision = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIShuntableComponent, AIShuntActionEvent>(OnAttemptShunt);

        SubscribeLocalEvent<StationAIShuntComponent, AIUnShuntActionEvent>(OnAttemptUnshunt);
        SubscribeLocalEvent<StationAIShuntComponent, GetVerbsEvent<AlternativeVerb>>(GetAltVerbs);

        SubscribeLocalEvent<StationAIShuntThroughComponent, GetVerbsEvent<AlternativeVerb>>(GetAltVerbs);
        SubscribeLocalEvent<StationAIShuntThroughComponent, FindShuntTargetEvent>(OnFindShuntTarget);
    }

    #region Actions
    private void OnAttemptShunt(EntityUid uid, StationAIShuntableComponent shuntable, AIShuntActionEvent ev)
    {
        if (ev.Handled)
            return;
        var target = ev.Target;
        if (_vision.IsOutsideCameraView(target))
            return;

        // If target has ShuntThrough component, search for a valid target in containers
        if (HasComp<StationAIShuntThroughComponent>(target))
        {
            var findEv = new FindShuntTargetEvent();
            RaiseLocalEvent(target, ref findEv);
            
            if (findEv.Target == null)
                return; // No valid target found

            target = findEv.Target.Value;
        }
        
        if (!TryComp<StationAIShuntComponent>(target, out var shunt))
            return;
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var _))
            return;
        if (!TryComp<MobStateComponent>(uid, out var state) || state.CurrentState != MobState.Alive)
            return;

        if (TryComp<BorgChassisComponent>(target, out var chassisComp))
        {
            var brain = chassisComp.BrainContainer.ContainedEntity;
            if (!brain.HasValue)
                return; //Chassis has no posibrian so cant shunt into it.
            if (!TryComp<StationAIShuntComponent>(brain, out var brainShunt))
                return; //Chassis brain is not able to be shunted into so obviously we cant.
            if (brainShunt.Return != null)
            {
                if (_net.IsServer) //only send on server cause client is confused somehow?
                    _popup.PopupEntity(Loc.GetString("shunt-target-occupied"), target, uid, PopupType.Large);
                return; //Chassis is allready inhabited.
            }
            brainShunt.Return = uid;
            brainShunt.ReturnAction = _actionSystem.AddAction(brain.Value, shuntable.UnshuntAction.Id);
        }
        if (shunt.Return != null)
        {
            if (_net.IsServer) //only send on server cause client is confused somehow?
                _popup.PopupEntity(Loc.GetString("shunt-target-occupied"), target, uid, PopupType.Large);
            return; //target is allready inhabited.
        }
        shunt.Return = uid;
        _mindSystem.TransferTo(mindId, target);
        shunt.ReturnAction = _actionSystem.AddAction(target, shuntable.UnshuntAction.Id);
        shuntable.Inhabited = target;

        if (TryComp<SiliconLawProviderComponent>(uid, out var coreLaws))
        {
            var getLaws = new GetSiliconLawsEvent(target);
            RaiseLocalEvent(target, ref getLaws);
            shunt.OldLawset = getLaws.Laws;

            _siliconLaw.SetLawset(target, coreLaws.Lawset);
        }

        EnsureComp<UncryoableComponent>(uid);

        var core = Transform(uid).ParentUid;
        if (TryComp<StationAiCoreComponent>(core, out var coreComp) &&
            TryComp<FollowedComponent>(coreComp.RemoteEntity, out var followed)
        )
        {
            foreach (var follower in followed.Following)
            {
                _follower.StartFollowingEntity(follower, target);
            }
        }
        Dirty(target, shunt);

        ev.Handled = true;
    }

    private void OnAttemptUnshunt(EntityUid uid, StationAIShuntComponent shunt, AIUnShuntActionEvent ev)
    {
        if (ev.Handled)
            return;

        if (!_mindSystem.TryGetMind(uid, out var mindId, out var _))
            return;
        
        var shuntActionUid = shunt.ReturnAction;
        var shuntReturnUid = shunt.Return;

        if (!TryComp<ActionComponent>(shuntActionUid, out var act))
            return; //Somehow the action does not have action component? invalid perhaps?

        if (!TryComp<StationAIShuntableComponent>(shuntReturnUid, out var shuntable))
            return; //trying to return to a body you cant leave from? weird...

        if (TryComp<BorgChassisComponent>(uid, out var chassisComp))
        {
            var brain = chassisComp.BrainContainer.ContainedEntity;
            if (!brain.HasValue)
                return; //Chassis has no brain... how is the AI controlling it???
            if (!TryComp<StationAIShuntComponent>(brain, out var brainShunt))
                return; //Chassis brain is not able to be shunted into so how is AI controlling it???
            
            var brainActionUid = brainShunt.ReturnAction;
            
            if (!TryComp<ActionComponent>(brainActionUid, out var brainAct))
                return; //Somehow the action does not have action component? invalid perhaps?
            _actionSystem.RemoveAction(new Entity<ActionComponent?>(brainActionUid.Value, brainAct));
            brainShunt.Return = null; //cause we are returning now
            brainShunt.ReturnAction = null;
        }

        _actionSystem.RemoveAction(new Entity<ActionComponent?>(shuntActionUid.Value, act));
        var target = shuntReturnUid.Value;
        _mindSystem.TransferTo(mindId, target);
        RemComp<UncryoableComponent>(target);

        var aiCore = Transform(target).ParentUid;
        if (TryComp<StationAiCoreComponent>(aiCore, out var core) &&
            core.RemoteEntity.HasValue
            )
        {
            _transform.SetMapCoordinates(core.RemoteEntity.Value,
                _transform.ToMapCoordinates(Transform(uid).Coordinates)
            );

            if (TryComp<FollowedComponent>(uid, out var followed))
            {
                foreach (var follower in followed.Following)
                {
                    _follower.StartFollowingEntity(follower, core.RemoteEntity.Value);
                }
            }
        }

        _siliconLaw.SetLawset(uid, shunt.OldLawset);

        shunt.ReturnAction = null;
        shunt.Return = null;
        shuntable.Inhabited = null;
    }
    #endregion

    #region Verbs
    public void GetAltVerbs(EntityUid uid, Component comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        // Handle unshunt verb for entities we're currently inhabiting
        if (comp is StationAIShuntComponent shuntComp && ev.User == ev.Target)
        {
            if (!shuntComp.Return.HasValue)
                return; //we are in something not inhabited. so obvs we cant shunt out of it.

            var unshuntVerb = new AlternativeVerb()
            {
                Text = Loc.GetString("ai-shunt-out-of"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")),
                Act = () =>
                {
                    var shuntEv = new AIUnShuntActionEvent()
                    {
                        Performer = uid
                    };
                    RaiseLocalEvent(uid, shuntEv);
                }
            };
            ev.Verbs.Add(unshuntVerb);
            return;
        }

        if (!HasComp<StationAIShuntableComponent>(ev.User))
            return; //only shuntable can get the into verb

        // Handle direct shunt targets
        if (comp is StationAIShuntComponent)
        {
            if (TryComp<BorgChassisComponent>(uid, out var chassis) && !HasComp<StationAIShuntComponent>(chassis.BrainContainer.ContainedEntity))
                return; //target borg chassis has no brain with shuntable component.
        }
        // Handle shunt-through targets
        else if (comp is StationAIShuntThroughComponent)
        {
            // Check if there's a valid shunt target inside
            var findEv = new FindShuntTargetEvent();
            RaiseLocalEvent(uid, ref findEv);
            
            if (findEv.Target == null)
                return; // No valid target found inside
        }
        else
        {
            return; // Unknown component type
        }

        var shuntVerb = new AlternativeVerb()
        {
            Text = Loc.GetString("ai-shunt-into"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")),
            Act = () =>
            {
                var shuntEv = new AIShuntActionEvent()
                {
                    Target = uid,
                    Performer = ev.User
                };
                RaiseLocalEvent(ev.User, shuntEv);
            }
        };
        ev.Verbs.Add(shuntVerb);
    }

    private void OnFindShuntTarget(EntityUid uid, StationAIShuntThroughComponent comp, ref FindShuntTargetEvent ev)
    {
        if (ev.Target != null)
            return; // Already found a target

        // Check if the container itself is a borg chassis with shuntable brain
        if (TryComp<BorgChassisComponent>(uid, out var selfChassis))
        {
            var brain = selfChassis.BrainContainer.ContainedEntity;
            if (brain.HasValue && HasComp<StationAIShuntComponent>(brain))
            {
                ev.Target = uid; // Return the chassis itself
                return;
            }
        }

        // Search through all containers for valid targets
        foreach (var container in _containers.GetAllContainers(uid))
        {
            foreach (var containedEntity in container.ContainedEntities)
            {
                // Check if this entity is directly shuntable
                if (HasComp<StationAIShuntComponent>(containedEntity))
                {
                    ev.Target = containedEntity;
                    return;
                }
                
                // Check if it's a borg chassis with shuntable brain
                if (TryComp<BorgChassisComponent>(containedEntity, out var chassis))
                {
                    var brain = chassis.BrainContainer.ContainedEntity;
                    if (brain.HasValue && HasComp<StationAIShuntComponent>(brain))
                    {
                        ev.Target = containedEntity;
                        return;
                    }
                }
                
                // Send event to nested containers
                RaiseLocalEvent(containedEntity, ref ev);
                if (ev.Target != null)
                    return;
            }
        }
    }
    #endregion
}

public sealed partial class AIShuntActionEvent : EntityTargetActionEvent
{
}

public sealed partial class AIUnShuntActionEvent : InstantActionEvent
{
}

[ByRefEvent]
public record struct FindShuntTargetEvent
{
    public EntityUid? Target;
}
