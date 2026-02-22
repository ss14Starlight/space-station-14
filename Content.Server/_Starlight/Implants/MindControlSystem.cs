using Content.Server.Popups;
using Content.Shared.Implants;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared._Starlight.Implants.Components;

namespace Content.Server._Starlight.Implants;

public sealed class MindControlSystem : EntitySystem
{
    private const string EscapeShuttleObjectiveId = "MindControlledEscapeShuttleObjective";
    private const string ProtectImplantObjectiveId = "MindControlledProtectImplant";
    private const string FollowOrdersObjectiveId = "MindControlledFollowOrders";
    
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    //[Dependency] private readonly TargetObjectiveSystem _targetObjectives = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    
   

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindControlImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<MindControlImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
    }

    private void OnImplantImplanted(EntityUid uid, MindControlImplantComponent component, ImplantImplantedEvent args)
    {
        AssignTraitorObjectives(args.Implanted, component);
        
        _popup.PopupEntity(Loc.GetString("mind-control-user-implanted"), args.Implanted, args.Implanted, PopupType.SmallCaution);
        if (TryComp<ActorComponent>(args.Implanted, out var actor)) //TODO make this something else? its clear at least
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg"), actor.PlayerSession, AudioParams.Default.WithVolume(1f));
        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
        
    }
    
    private void OnImplantRemoved(EntityUid uid, MindControlImplantComponent component, ImplantRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Implanted))
            return;
        
        RemoveTraitorObjectives(args.Implanted);
        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
    }
    
    private void RemoveTraitorObjectives(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind)) 
            return;
        _mind.TryFindObjective(mindId, EscapeShuttleObjectiveId, out var objectiveEscape);
        if (objectiveEscape != null) _mind.TryRemoveObjective(mindId, mind, objectiveEscape.Value);

        _mind.TryFindObjective(mindId, ProtectImplantObjectiveId, out var objectiveProtect);
        if (objectiveProtect != null) _mind.TryRemoveObjective(mindId, mind, objectiveProtect.Value);
        
        _mind.TryFindObjective(mindId, FollowOrdersObjectiveId, out var objectiveOrders);
        if (objectiveOrders != null) _mind.TryRemoveObjective(mindId, mind, objectiveOrders.Value);
       
        _popup.PopupEntity(Loc.GetString("mind-control-user-freed"), uid, uid, PopupType.Medium);
        
    }

    private void AssignTraitorObjectives(EntityUid implanted, MindControlImplantComponent component)
    {
        if (!_mind.TryGetMind(implanted, out var mindId, out var mind))
            return;
        var objectiveEscape = _objectives.TryCreateObjective(mindId, mind, EscapeShuttleObjectiveId);
        var objectiveProtect = _objectives.TryCreateObjective(mindId, mind, ProtectImplantObjectiveId);
        var objectiveOrders =  _objectives.TryCreateObjective(mindId, mind, FollowOrdersObjectiveId);
        
        
        if(objectiveEscape == null)
            return;
        _mind.AddObjective(mindId, mind, objectiveEscape.Value);
        
        if (objectiveProtect == null)
            return;
        _mind.AddObjective(mindId, mind, objectiveProtect.Value);
        
        if (objectiveOrders == null)
            return;
        //_targetObjectives.SetTarget(objectiveOrders.Value, component.Master); //TODO Figure out why this won't set targets...
        _mind.AddObjective(mindId, mind, objectiveOrders.Value);
        
    }
}