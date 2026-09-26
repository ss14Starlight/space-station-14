using Content.Server.Antag;
using Content.Server.Popups;
using Content.Shared.Implants;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mindshield.Components;

namespace Content.Server._Starlight.Implants;
public sealed partial class MindControlSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private RoleSystem _role = default!;

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

        if (!_mind.TryGetMind(args.Target, out _, out var mind) || _mind.IsCharacterDeadIc(mind))
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

    /// <summary>
    /// Event that is called when a mind control implant is used on a player
    /// </summary>
    private void OnImplantImplanted(EntityUid uid, MindControlImplantComponent component, ImplantImplantedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        var masterName = Exists(component.Master) ? Name(component.Master) : "Unknown";

        _antag.SendBriefing(actor.PlayerSession, Loc.GetString(component.BriefingText, ("master-name", masterName)), null, component.BriefingSound);
        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
        AssignTraitorObjectives(args.Implanted);

    }

    /// <summary>
    /// Event that is called when a mind control implant is extracted from a player
    /// </summary>
    private void OnImplantRemoved(EntityUid uid, MindControlImplantComponent component, ImplantRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Implanted))
            return;

        if (!TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        _antag.SendBriefing(actor.PlayerSession, Loc.GetString(component.DebriefingText), null, null);
        RemoveTraitorObjectives(args.Implanted);
        _status.TryAddStatusEffectDuration(args.Implanted, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(2));
    }

    private void OnEmpPulse(Entity<MindControlComponent> ent, ref EmpPulseEvent args)
    {
        EnsureComp<StaminaComponent>(ent.Owner, out var stamina);
        _staminaSystem.TakeStaminaDamage(ent.Owner, stamina.CritThreshold, stamina);

        args.Affected = true;
        args.Disabled = true;
    }

    /// <summary>
    /// Attempts to remove the mind control component, objective, and role
    /// </summary>
    private void RemoveTraitorObjectives(EntityUid uid)
    {
        if (!TryComp<MindControlComponent>(uid, out var comp))
            return;

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            //Remove objectives
            if (_mind.TryFindObjective((mindId, mind), comp.ObeyObjectiveId, out var objective))
                _mind.TryRemoveObjective(mindId, mind, objective.Value);
            //Remove role
            _role.MindRemoveRole<MindControlComponent>(mindId);
        }
        RemComp<MindControlComponent>(uid);

        _popup.PopupEntity(Loc.GetString("mind-control-user-freed"), uid, uid, PopupType.Medium);

    }

    /// <summary>
    /// Attempts to apply the mind controlled comp, objective, and role
    /// </summary>
    private void AssignTraitorObjectives(EntityUid implanted)
    {
        if (!_mind.TryGetMind(implanted, out var mindId, out var mind))
            return;

        var mindControlComp = AddComp<MindControlComponent>(implanted);
        _role.MindAddRole(mindId, mindControlComp.MindRoleId, mind, true);
        _mind.TryAddObjective(mindId, mind, mindControlComp.ObeyObjectiveId);
    }
}
