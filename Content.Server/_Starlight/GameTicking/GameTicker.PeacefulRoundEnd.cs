using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._NullLink;
using Content.Shared._Starlight.GameTicking.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Starlight;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Starlight.GameTicking;

public sealed class PeacefulRoundEndSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedNullLinkPlayerRolesReqManager _rolesReq = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

    private bool _isEnabled = false;
    private bool _roundedEnded = false;


    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.PeacefulRoundEnd, v => _isEnabled = v, true);

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<GotRehydratedEvent>(OnRehydrateEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<EorgActionComponent, ActionValidateEvent>(OnValidatePossiblyEorgAction);
    }

    private void SpreadPeace(EntityUid target)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (_rolesReq.IsPeacefulBypass(target)) return; // OOC bypass (staff, extroles, ..)
        if (!IsGridPacificationTarget(target)) return; // Only pacify people on Evac and CC grids.
        if (IsMindRolePacificationImmune(target)) return; // IC bypass (taken roles of ERT, Decimus, CC, ..)
        if (IsGhostRolePacificationImmune(target)) return; // IC bypass (same as previous, only when ghost role wasn't taken)

        EnsureComp<PacifiedComponent>(target);
        EnsureComp<PreventEorgComponent>(target);
    }

    /// <summary>
    /// Checks if the entity has any mind roles that are exempt from pacification.
    /// </summary>
    private bool IsMindRolePacificationImmune(EntityUid uid)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) ||
            !TryComp<MindComponent>(mindContainer.Mind, out var mind))
            return false;

        foreach (var role in _role.MindGetAllRoleInfo((mindContainer.Mind.Value, mind)))
        {
            if (role.Antagonist)
                continue;
            if (!_proto.TryIndex<JobPrototype>(role.Prototype, out var mindJob))
                continue;
            if (mindJob.BypassEorPacification)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the entity has any ghost roles that are exempt from pacification.
    /// </summary>
    private bool IsGhostRolePacificationImmune(EntityUid uid)
    {
        if (!TryComp<GhostRoleComponent>(uid, out var ghostRole) ||
            !_proto.TryIndex(ghostRole.JobProto, out var job))
            return false;
        return job.BypassEorPacification;
    }

    /// <summary>
    /// Check whether a grid is a target for pacification. Returns true for Evac and CentComm only.
    /// </summary>
    private bool IsGridPacificationTarget(EntityUid uid)
    {
        var xform = Transform(uid);
        var grid = xform.GridUid;

        if (HasComp<EmergencyShuttleComponent>(grid))
            return true; // Evac shuttle (escape pods don't count for this) = pacified

        AllEntityQuery<StationCentcommComponent>().MoveNext(out var centcomm);
        if (centcomm != null && centcomm.Entity == grid)
            return true; // CC = pacified

        // In all other cases we do not *mechanically* enfore it.
        // This way station-ending antags can still do their thing,
        // and sec can still fight back if they're left behind on station.
        return false;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
        => SpreadPeace(ev.Mob);

    private void OnRehydrateEvent(ref GotRehydratedEvent ev)
        => SpreadPeace(ev.Target);

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
        => _roundedEnded = false;

    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        _roundedEnded = true;

        var mobMoverQuery = EntityQueryEnumerator<MobMoverComponent>();
        while (mobMoverQuery.MoveNext(out var uid, out _))
            SpreadPeace(uid);

        var mechQuery = EntityQueryEnumerator<MechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
            SpreadPeace(uid);
    }

    private void OnValidatePossiblyEorgAction(EntityUid uid, EorgActionComponent component, ref ActionValidateEvent args)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (!TryComp<PreventEorgComponent>(args.User, out _))  return;

        _popup.PopupEntity(Loc.GetString("eorg-action"), args.User, args.User, PopupType.LargeCaution);
        args.Invalid = true;
    }
}
