using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Polymorph.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._NullLink;
using Content.Shared._Starlight.EndOfRoundGriefing;
using Content.Shared._Starlight.EndOfRoundGriefing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Polymorph;
using Content.Shared.Roles;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.EndOfRoundGriefing;

/// <summary>
/// <inheritdoc/>
///
/// The server side of the EORG prevent system manages state and is responsible for managing the <see cref="PreventEorgComponent"/>s on players.
/// </summary>
public sealed partial class EorgPreventionSystem : SharedEorgPreventionSystem
{
    [Dependency] private SharedRoleSystem _role = null!;
    [Dependency] private IChatManager _chat = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private ISharedNullLinkPlayerRolesReqManager _rolesReq = null!;

    private readonly List<EntityUid> _pendingPacification = [];
    private readonly List<EntityUid> _pacified = [];

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.PeacefulRoundEnd, v =>
        {
            IsEnabled = v;
            BroadcastState();
        }, true);
    }

    private void BroadcastState() =>
        RaiseNetworkEvent(new EorgPreventionStateEvent(IsEnabled, HasRoundEnded, IsPending));

    /// <summary>
    /// Validate an entity is eligible for pacification, and applies it if so.
    /// </summary>
    /// <param name="target">The entity to potentially pacify</param>
    private void SpreadPeaceNow(EntityUid target)
    {
        if (!IsEnabled || !HasRoundEnded) return;

        // Filters out ineligible targets and adds polymorph parents.
        var targets = new List<EntityUid>();
        SpreadPeaceQueueing(target, ref targets);

        if (IsPending)
        {
            _pendingPacification.AddRange(targets);
            return;
        }

        foreach (var uid in targets)
            Pacify(uid);
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
        while (TryComp<PolymorphedEntityComponent>(target, out var polymorph)
            && polymorph.Parent is { } parent && !TerminatingOrDeleted(parent))
        {
            target = parent;
            candidates.Add(target);
        }
    }

    /// <summary>
    /// Removes candidates that are part of a polymorph chain where some immunity (OOC or job immunity) is at play.
    /// </summary>
    /// <param name="candidates">The pacification candidates to filter</param>
    private void RemoveImmunePolymorphChains(ref List<EntityUid> candidates)
    {
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
        if (!HasComp<PacifiedComponent>(target))
        {
            AddComp<PacifiedComponent>(target);
            _pacified.Add(target);
        }

        EnsureComp<PreventEorgComponent>(target);
    }

    private void BlockPacification()
    {
        IsPending = true;
        foreach (var uid in _pacified.Where(uid => !TerminatingOrDeleted(uid)))
        {
            RemComp<PacifiedComponent>(uid);
            RemComp<PreventEorgComponent>(uid);
            _pendingPacification.Add(uid);
        }

        _pacified.Clear();
        BroadcastState();
        _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-reblocked"), Color.Red);
    }

    private void PacifyPending()
    {
        IsPending = false;
        BroadcastState();
        _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-unblocked"), Color.LimeGreen);

        // Mobs can reach Evac or CC while pacification is blocked, so the queue alone isn't enough.
        var candidates = CollectPacificationCandidates();
        candidates.AddRange(_pendingPacification.Except(candidates).ToList());
        _pendingPacification.Clear();
        RemoveImmunePolymorphChains(ref candidates);

        // The last threat may be mid-deletion, and the scan above still sees it.
        foreach (var uid in candidates.Where(uid => !TerminatingOrDeleted(uid)))
            Pacify(uid);
    }

    private List<EntityUid> CollectPacificationCandidates()
    {
        var candidates = new List<EntityUid>();

        var mobMoverQuery = EntityQueryEnumerator<MobMoverComponent>();
        while (mobMoverQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        var mechQuery = EntityQueryEnumerator<MechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
            SpreadPeaceQueueing(uid, ref candidates);

        return candidates;
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

        foreach (var role in _role.MindGetAllRoleInfo((mindContainer.Mind.Value, mind)).Where(role => !role.Antagonist))
        {
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
        // CC = pacified
        // In all other cases we do not *mechanically* enforce it.
        // This way station-ending antags can still do their thing,
        // and sec can still fight back if they're left behind on station.
        return centcomm != null && centcomm.Entity == grid;
    }

    #endregion

    #region Event handlers

    [SubscribeLocalEvent]
    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
        => SpreadPeaceNow(ev.Mob);

    [SubscribeLocalEvent]
    private void OnRehydrateEvent(ref GotRehydratedEvent ev)
        => SpreadPeaceNow(ev.Target);

    [SubscribeLocalEvent]
    private void OnRoundEndCancelled(RoundEndCancelMessageEvent ev)
    {
        foreach (var uid in _pacified.Where(uid => !TerminatingOrDeleted(uid)))
        {
            RemComp<PacifiedComponent>(uid);
            RemComp<PreventEorgComponent>(uid);
        }

        ReleaseEmergencyAccess();
        ResetState();
    }

    [SubscribeLocalEvent]
    private void OnRoundCleanup(RoundRestartCleanupEvent ev) => ResetState();

    private void ResetState()
    {
        HasRoundEnded = false;
        IsPending = false;
        _activeThreats.Clear();
        _pendingPacification.Clear();
        _pacified.Clear();
        _nukeCheckAccumulator = TimeSpan.Zero;
        _ccHadDefenders = false;
        _announced.Clear();
        _emergencyAccessAirlocks.Clear();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        BroadcastState();
    }

    [SubscribeLocalEvent]
    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        HasRoundEnded = true;
        BroadcastState();
        if (!IsEnabled) return;

        var candidates = CollectPacificationCandidates();

        // Remove any candidates that are part of a polymorph chain where some immunity (OOC or job immunity) is at play.
        RemoveImmunePolymorphChains(ref candidates);

        // Now that we have all candidates, that means we also have anyone who would be an active threat too.
        var area = GetThreatArea();
        _ccHadDefenders = FindCentCommDefenders(GetCentCommGrid());
        var zeds = FindZeds(area);
        var nukeOps = FindNukies(area);

        _activeThreats = [.. zeds, .. nukeOps];

        if (_activeThreats.Count > 0)
        {
            UpdateThreatAlerts();
            IsPending = true;
            foreach (var uid in candidates)
                _pendingPacification.Add(uid);
            BroadcastState();
            _chat.DispatchServerAnnouncement(Loc.GetString("eor-pacifism-blocked"), Color.Red);
            return;
        }

        // Pacify the remaining candidates.
        foreach (var uid in candidates)
            Pacify(uid);
    }

    /// <summary>
    /// If someone with <see cref="PreventEorgComponent"/> or awaiting pacification polymorphs, carry that over to their
    /// polymorph. <see cref="PolymorphedEvent"/> is only raised directed, and queued entities have no marker component.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPolymorphed(Entity<MetaDataComponent> ent, ref PolymorphedEvent ev)
    {
        if (!_pendingPacification.Contains(ent) && !HasComp<PreventEorgComponent>(ent))
            return;

        if (!IsPending)
        {
            Pacify(ev.NewEntity);
            return;
        }

        if (!_pendingPacification.Contains(ev.NewEntity))
            _pendingPacification.Add(ev.NewEntity);
    }

    [SubscribeLocalEvent]
    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        _pacified.Remove(args.Entity);
        RemoveThreat(args.Entity);
    }

    [SubscribeNetworkEvent]
    private void OnRequestEorgPreventionState(RequestEorgPreventionStateEvent msg, EntitySessionEventArgs args) =>
        RaiseNetworkEvent(new EorgPreventionStateEvent(IsEnabled, HasRoundEnded, IsPending));

    #endregion
}
