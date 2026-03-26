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
        SubscribeLocalEvent<AntagonisticActionComponent, ActionValidateEvent>(OnValidateAntagonisticAction);
    }

    private void SpreadPeace(EntityUid target)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (_rolesReq.IsPeacefulBypass(target)) return; // OOC bypass (staff, extroles, ..)
        if (!IsOnPacifiedGrid(target)) return; // Only pacify people on Evac and CC grids.
        if (IsMindRolePacificationImmune(target)) return; // IC bypass (BSO, ERT, Decimus, CC, ..)
        if (IsGhostRolePacificationImmune(target)) return; // IC bypass (same as previous, only when ghost role wasn't taken)
        
        EnsureComp<PacifiedComponent>(target);
        EnsureComp<DisableAntagonismComponent>(target);
    }

    private bool IsMindRolePacificationImmune(EntityUid uid)
    {
        // Checks if the mind has roles that are exempt from pacification.
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer))
            return false;
        if (!TryComp<MindComponent>(mindContainer.Mind, out var mind))
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

    private bool IsGhostRolePacificationImmune(EntityUid uid)
    {
        // If we don't find any in the mind, check for ghost role jobs.
        if (!TryComp<GhostRoleComponent>(uid, out var ghostRole))
            return false;
        if (!_proto.TryIndex(ghostRole.JobProto, out var job))
            return false;
        return job.BypassEorPacification;
    }
    
    private bool IsOnPacifiedGrid(EntityUid uid)
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
    
    private void OnValidateAntagonisticAction(EntityUid uid, AntagonisticActionComponent component, ref ActionValidateEvent args)
    {
        if (!_isEnabled)
            return;
        if (!TryComp<DisableAntagonismComponent>(args.User, out _))
            return;
        
        _popup.PopupEntity(Loc.GetString("peaceful-round-end"), args.User, args.User, PopupType.LargeCaution);
        args.Invalid = true;
    }
}
