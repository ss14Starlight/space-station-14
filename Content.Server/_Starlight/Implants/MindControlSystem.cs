using Content.Server.Antag;
using Content.Server.Popups;
using Content.Shared.Implants;
using Content.Server.Objectives;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mindshield.Components;


namespace Content.Server._Starlight.Implants;

public sealed class MindControlSystem : EntitySystem
{
    private const string FollowOrdersObjectiveId = "MindControlledFollowOrders";

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    //[Dependency] private readonly TargetObjectiveSystem _targetObjectives = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;



public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindControlImplantComponent, AddImplantAttemptEvent>(OnAttemptImplant);
        SubscribeLocalEvent<MindControlImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<MindControlImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
        SubscribeLocalEvent<MindControlComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnAttemptImplant(EntityUid uid, MindControlImplantComponent component, AddImplantAttemptEvent args)
    {
        if (args.User == args.Target)
        {
            //Covered by another popup
            args.Cancel();
        }

        if (!_mind.TryGetMind(args.Target, out var mindId, out var mind) || _mind.IsCharacterDeadIc(mind))
        {
            _popup.PopupEntity(Loc.GetString("mind-control-invalid"), args.User, args.User, PopupType.Small);
            args.Cancel();
        }

        if (HasComp<MindShieldComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("mind-control-prevented"), args.User, args.User, PopupType.Small);
            args.Cancel();
        }
        component.Master = args.User;
    }

    private void OnImplantImplanted(EntityUid uid, MindControlImplantComponent component, ImplantImplantedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        if (!_mind.TryGetMind(component.Master, out var masterMindId, out var masterMind))
            return;

        if (masterMind.CharacterName == null)
            return;

        AddComp<MindControlComponent>(args.Implanted);
        _antag.SendBriefing(actor.PlayerSession, Loc.GetString(component.BriefingText, ("master-name", masterMind.CharacterName)), null, component.BriefingSound);

        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
        AssignTraitorObjectives(args.Implanted, component);

    }

    private void OnImplantRemoved(EntityUid uid, MindControlImplantComponent component, ImplantRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Implanted))
            return;

        if (!TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        _antag.SendBriefing(actor.PlayerSession, Loc.GetString(component.DebriefingText), null, null);
        RemoveTraitorObjectives(args.Implanted);
        RemCompDeferred<MindControlComponent>(args.Implanted);
        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
    }

    private void OnEmpPulse(Entity<MindControlComponent> ent, ref EmpPulseEvent args)
    {
        EnsureComp<StaminaComponent>(ent.Owner, out var stamina);
        _staminaSystem.TakeStaminaDamage(ent.Owner, stamina.CritThreshold, stamina);

        args.Affected = true;
        args.Disabled = true;
    }

    private void RemoveTraitorObjectives(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        _mind.TryFindObjective(mindId, FollowOrdersObjectiveId, out var objectiveOrders);
        if (objectiveOrders != null) _mind.TryRemoveObjective(mindId, mind, objectiveOrders.Value);

        _popup.PopupEntity(Loc.GetString("mind-control-user-freed"), uid, uid, PopupType.Medium);

    }

    private void AssignTraitorObjectives(EntityUid implanted, MindControlImplantComponent component)
    {
        if (!_mind.TryGetMind(implanted, out var mindId, out var mind))
            return;

        var objectiveOrders =  _objectives.TryCreateObjective(mindId, mind, FollowOrdersObjectiveId);

        if (objectiveOrders == null)
            return;

        _mind.AddObjective(mindId, mind, objectiveOrders.Value);
    }
}
