using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Polymorph.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._NullLink;
using Content.Shared._Starlight.GameTicking.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Roles;
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
        SubscribeLocalEvent<PreventEorgComponent, PolymorphedEvent>(OnPolymorphed);
    }

    /// <summary>
    /// Validate an entity is eligible for pacification, and applies it if so.
    /// </summary>
    /// <param name="target">The entity to potentially pacify</param>
    private void SpreadPeaceNow(EntityUid target)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (!ShouldPacify(target)) return;
        Pacify(target);

        // If the entity is polymorphed, also pacify the parent(s).
        while (TryComp<PolymorphedEntityComponent>(target, out var polymorph) && polymorph.Parent != null)
        {
            target = polymorph.Parent.Value;
            Pacify(target);
        }
    }

    /// <summary>
    /// The queueing counterpart of <see cref="SpreadPeaceNow"/>. Checks if the target is a valid pacification candidate
    /// and adds them to the candidate list if they are valid pacification.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="candidates"></param>
    private void SpreadPeaceQueueing(EntityUid target, ref List<EntityUid> candidates)
    {
        if (!ShouldPacify(target)) return;
        candidates.Add(target);

        // If the entity is polymorphed, also pacify the parent(s).
        while (TryComp<PolymorphedEntityComponent>(target, out var polymorph) && polymorph.Parent != null)
        {
            target = polymorph.Parent.Value;
            candidates.Add(target);
        }
    }

    /// <summary>
    /// Determine if the target should be pacified. Does not do any polymorph-related checks.
    /// </summary>
    /// <param name="target">The entity to test</param>
    /// <returns>Whether the target should be pacified, not accounting or polymorphs</returns>
    private bool ShouldPacify(EntityUid target)
    {
        // OOC bypass (staff, extroles, ...).
        if (_rolesReq.IsPeacefulBypass(target))
            return false;

        // Only pacify people on Evac and CC grids.
        if (!IsGridPacificationTarget(target))
            return false;

        // IC bypasses only apply to a specific mind roles (taken roles of ERT, Decimus, CC, ...).
        // Note that in the case of polymorphs, the mind and thus the mind roles are only contained in the child entity
        // (the one that is being actively controlled).
        if (IsMindRolePacificationImmune(target))
            return false;

        // IC bypass (only when ghost role wasn't taken)
        return !IsGhostRolePacificationImmune(target);
    }

    /// <summary>
    /// Pacify a target immediately.
    /// </summary>
    /// <param name="target">The target to pacify</param>
    private void Pacify(EntityUid target)
    {
        EnsureComp<PacifiedComponent>(target);
        EnsureComp<PreventEorgComponent>(target);
    }

    #region Pacification checks

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

    #endregion
    #region Event handlers

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
        => SpreadPeaceNow(ev.Mob);

    private void OnRehydrateEvent(ref GotRehydratedEvent ev)
        => SpreadPeaceNow(ev.Target);

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
        => _roundedEnded = false;

    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        _roundedEnded = true;
        if (!_isEnabled) return;

        var candidates = new List<EntityUid>();

        // Collect candidate entities for pacification.
        var mobMoverQuery = EntityQueryEnumerator<MobMoverComponent>();
        while (mobMoverQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        var mechQuery = EntityQueryEnumerator<MechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        // Remove any candidates that are part of a polymorph chain where some immunity (OOC or job immunity) is at play.
        var polymorphedQuery = EntityQueryEnumerator<PolymorphedEntityComponent>();
        while (polymorphedQuery.MoveNext(out var uid, out _))
        {
            // Loop from the current node up the polymorph tree.
            var current = uid;
            var deleting = false;
            while (TryComp<PolymorphedEntityComponent>(current, out var polymorphed) && polymorphed.Parent.HasValue)
            {
                if (deleting || !candidates.Contains(current))
                {
                    deleting = true;
                    candidates.Remove(polymorphed.Parent.Value);
                }
                current = polymorphed.Parent.Value;
            }
        }

        // Pacify the remaining candidates.
        foreach (var uid in candidates)
            Pacify(uid);
    }

    /// <summary>
    /// Prevents performing actions marked as <see cref="EorgActionComponent"/> if the user has
    /// <see cref="PreventEorgComponent"/>.
    /// </summary>
    private void OnValidatePossiblyEorgAction(EntityUid uid, EorgActionComponent component, ref ActionValidateEvent args)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (!HasComp<PreventEorgComponent>(args.User))  return;

        _popup.PopupEntity(Loc.GetString("eorg-action"), args.User, args.User, PopupType.LargeCaution);
        args.Invalid = true;
    }


    /// <summary>
    /// If someone with <see cref="PreventEorgComponent"/> polymorphs, also apply it to their polymorph.
    /// </summary>
    private void OnPolymorphed(EntityUid uid, PreventEorgComponent comp, PolymorphedEvent ev) => Pacify(ev.NewEntity);

    #endregion

}
