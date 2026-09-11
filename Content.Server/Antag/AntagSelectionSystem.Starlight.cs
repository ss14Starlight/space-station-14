using Prometheus;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Server.Antag.Components.AntagSelectionTime;

namespace Content.Server.Antag;

public partial class AntagSelectionSystem
{

    #region Data collection
    // Metrics for antag selection and spawning. These are used to track how many antags are spawned, how many are selected, and how many are actually assigned to players.
    private static readonly Counter _antagsSpawned = Metrics.CreateCounter(
        "sl_antags_spawned",
        "Number of antagonists spawned by type",
        ["type"]
    );

    private static readonly Gauge _antagSelectionCounts = Metrics.CreateGauge(
        "sl_antag_selection_count",
        "Antagonist selection counts by round, game rule, antagonist type, and state",
        ["round", "rule", "type", "state"]
    );

    private readonly HashSet<Gauge.Child> _antagSelectionMetricChildren = [];

    /// <summary>
    /// Stores the initial stats of a game rule's antag selection, used for logging and debugging.
    /// </summary>
    private readonly record struct InitialAntagSelectionStats(
            Entity<AntagSelectionComponent> GameRule,
            AntagSpecifierPrototype Definition,
            int Target,
            int Eligible);
    #endregion


    // This is a safety check to ensure that all antags have been assigned to players, and if not, we will try to assign them again.
    protected override void ActiveTick(EntityUid uid,
        AntagSelectionComponent component,
        GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        // Covers rules activated in the small window after the spawning events but before the
        // ticker changes to InRound. They must not wait for the five-minute safety audit.
        if (!component.AssignmentHandled)
        {
            EnforceAntagTargets((uid, component), GetActivePlayers().ToArray());
            component.AssignmentHandled = true;
            return;
        }

        if (component.NextSelectionAudit is not { } nextAudit ||
            Timing.CurTime < nextAudit)
        {
            return;
        }

        var shouldRetry = EnforceAntagTargets((uid, component), GetActivePlayers().ToArray());
        if (!shouldRetry)
        {
            component.NextSelectionAudit = null;
            return;
        }

        var maxRetries = Math.Max(0, component.MaxSelectionAuditRetries);
        if (component.SelectionAuditRetries >= maxRetries)
        {
            Log.Error($"Antag selection audit for {ToPrettyString(uid)} exhausted its configured retry limit of {maxRetries}.");
            component.NextSelectionAudit = null;
            return;
        }

        component.SelectionAuditRetries++;
        component.NextSelectionAudit = Timing.CurTime + SelectionAuditRetryDelay;
    }

    /// <summary>
    /// Releases either every failed preselection for a player, or only the specified antags.
    /// Also removes released reservations from the post-spawn initialization queue so the same
    /// vacancy is not processed and queued a second time by <see cref="OnJobsAssigned"/>.
    /// </summary>
    private void ReleaseFailedPreSelections(
        ICommonSession player,
        IReadOnlySet<ProtoId<AntagSpecifierPrototype>>? antags = null)
    {
        var released = new HashSet<(EntityUid Rule, ProtoId<AntagSpecifierPrototype> Antag)>();

        // We only care about delayed rules, since active rules initialize their antags immediately.
        var query = QueryDelayedRules();
        while (query.MoveNext(out var uid, out _, out var comp, out _))
        {
            if (comp.SelectionTime == RuleStarted)
                continue;

            Debug.Assert(comp.SelectionTime != Never,
                $"Player: {player.Name}, was pre-selected for a game rule {ToPrettyString(uid)} which does not do pre-selections");

            if (!comp.RemoveUponFailedSpawn)
                continue;

            foreach (var selector in comp.Antags)
            {
                var antag = selector.Proto;
                if (antags != null && !antags.Contains(antag))
                    continue;

                if (!comp.PreSelectedSessions.TryGetValue(antag, out var sessions) ||
                    !sessions.Contains(player))
                {
                    continue;
                }

                DeSelectSession((uid, comp), antag, player, sessions);
                QueueReplacement((uid, comp), antag);
                released.Add((uid, antag));
            }
        }

        if (released.Count == 0)
            return;

        _delayedAntags.RemoveAll(entry =>
            entry.player.UserId == player.UserId &&
            released.Contains((entry.gameRule.Owner, entry.antag.ID)));
    }

    private void AddGameRuleDefinitions(Entity<AntagSelectionComponent> gameRule,
        int playerCount,
        ref List<AntagRule> preSpawnRoles,
        ref List<AntagRule> postSpawnRoles,
        bool active)
    {
        switch (gameRule.Comp.SelectionTime)
        {
            case PrePlayerSpawn:
                AddGameRuleDefinitions(gameRule, playerCount, ref preSpawnRoles, active);
                break;
            case JobsAssigned:
                AddGameRuleDefinitions(gameRule, playerCount, ref postSpawnRoles, active);
                break;
            case RuleStarted:
                if (active) // Only if the game rule is active to we preselect, since the event for activation already ran and was skipped.
                    AddGameRuleDefinitions(gameRule, playerCount, ref postSpawnRoles, active);
                break;
            case Never:
                SpawnGhostRoles(gameRule, playerCount, true);
                break;
        }
    }

    #region Multi-slot
    // This entire section wouldn't exist without multi-slot.

    /// <summary>
    /// Records a slot that was reserved during pre-selection but was not successfully assigned.
    /// The slot is not considered filled until a replacement initializes or a ghost role reserves it.
    /// </summary>
    private void QueueReplacement(
        Entity<AntagSelectionComponent> gameRule,
        ProtoId<AntagSpecifierPrototype> definition) =>
        gameRule.Comp.PendingReplacements[definition] =
            gameRule.Comp.PendingReplacements.GetValueOrDefault(definition) + 1;

    /// <summary>
    /// Attempts to fill every failed pre-selection with another eligible live player. Only successful
    /// initialization decrements the vacancy count; any remainder is explicitly reserved by ghost roles.
    /// </summary>
    private void AssignPendingReplacements(
        Entity<AntagSelectionComponent> gameRule,
        IList<ICommonSession> players,
        int playerCount)
    {
        foreach (var (proto, vacancies) in gameRule.Comp.PendingReplacements.ToArray())
        {
            if (!Proto.Resolve(proto, out var definition))
                continue;

            var assigned = GetAssignedAntagCount(gameRule, proto);
            var pendingGhostRoles = GetPendingAntagGhostRoleCount(gameRule, proto);
            var target = Math.Max(
                gameRule.Comp.SelectionTargets.GetValueOrDefault(proto),
                GetTargetAntagCount(gameRule, playerCount, proto));
            var replacements = Math.Min(vacancies, Math.Max(0, target - assigned - pendingGhostRoles));

            if (replacements <= 0)
                continue;

            var weightedPool = GetWeightedPlayerPool(players);
            while (replacements > 0 && RobustRandom.TryPickAndTake(weightedPool, out var session))
            {
                if (TryMakeAntag(gameRule, definition, session))
                    replacements--;
            }

            SpawnGhostRoles(gameRule, definition, replacements);
        }

        gameRule.Comp.PendingReplacements.Clear();
    }
    #endregion

    /// <summary>
    /// Calculates the initial antag selection stats for a list of players and game rules.
    /// </summary>
    private List<InitialAntagSelectionStats> GetInitialAntagSelectionStats(
        IEnumerable<ICommonSession> players,
        List<AntagRule> rules)
    {
        var playerArray = players as ICommonSession[] ?? players.ToArray();
        var stats = new List<InitialAntagSelectionStats>(rules.Count);

        foreach (var rule in rules)
        {
            if (!rule.Definition.PickPlayer)
                continue;

            var eligible = 0;

            foreach (var player in playerArray)
            {
                if (!CanBeAntag(player, rule.GameRule, rule.Definition))
                    continue;

                if (!_pref.TryGetCachedPreferences(player.UserId, out var preferences))
                    continue;

                var hasValidProfile = preferences.Characters.Values
                    .OfType<HumanoidCharacterProfile>()
                    .Any(profile =>
                        profile.Enabled &&
                        IsProfileValidForAntag(player, profile, rule.Definition));

                if (hasValidProfile)
                    eligible++;
            }

            stats.Add(new InitialAntagSelectionStats(rule.GameRule, rule.Definition, rule.Count, eligible));
        }

        return stats;
    }

    /// <summary>
    /// Logs the initial antag selection stats for a list of game rules and their antag definitions.
    /// </summary>
    private void LogInitialAntagSelectionStats(IEnumerable<InitialAntagSelectionStats> stats)
    {
        foreach (var stat in stats)
        {
            var preselected = stat.GameRule.Comp.PreSelectedSessions.TryGetValue(stat.Definition.ID, out var sessions) ? sessions.Count : 0;
            var assigned = GetAssignedAntagCount(stat.GameRule, stat.Definition.ID);
            var ghostRoles = GetPendingAntagGhostRoleCount(stat.GameRule, stat.Definition.ID);
            var unfilled = Math.Max(0, stat.Target - preselected - ghostRoles);
            var message = $"{stat.Definition.ID}: target={stat.Target}, eligible={stat.Eligible}, " +
                $"preselected={preselected}, assigned={assigned}, ghostRoles={ghostRoles}, unfilled={unfilled}. " +
                $"Gamerule: {ToPrettyString(stat.GameRule)}";

            UpdateAntagSelectionMetrics(stat.GameRule, stat.Definition, stat.Target, assigned, ghostRoles);
            Log.Info(message);
            _adminLogger.Add(LogType.AntagSelection, $"{message}");
        }
    }

    /// <summary>
    /// Gets an antag-selection metric child and tracks it for removal after the round.
    /// </summary>
    private Gauge.Child GetAntagSelectionMetric(string round, string rule, string type, string state)
    {
        var metric = _antagSelectionCounts.WithLabels(round, rule, type, state);
        _antagSelectionMetricChildren.Add(metric);
        return metric;
    }

    /// <summary>
    /// Removes the completed round's metric children so they are not retained for the server's lifetime.
    /// </summary>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        foreach (var metric in _antagSelectionMetricChildren)
        {
            metric.Remove();
        }

        _antagSelectionMetricChildren.Clear();
    }

    /// <summary>
    /// Updates the antag selection metrics for a given game rule and antag definition.
    /// Just for logging, pretty much.
    /// </summary>
    private void UpdateAntagSelectionMetrics(
        Entity<AntagSelectionComponent> gameRule,
        AntagSpecifierPrototype definition,
        int expected,
        int assigned,
        int ghostRoles,
        int forcedAssignments = 0,
        int ghostRolesCreated = 0)
    {
        var round = GameTicker.RoundId.ToString(CultureInfo.InvariantCulture);
        var rule = MetaData(gameRule).EntityPrototype?.ID ?? "unknown";
        var type = definition.ID;

        GetAntagSelectionMetric(round, rule, type, "expected").Set(expected);
        GetAntagSelectionMetric(round, rule, type, "assigned").Set(assigned);
        GetAntagSelectionMetric(round, rule, type, "ghost_roles").Set(ghostRoles);
        GetAntagSelectionMetric(round, rule, type, "unassigned").Set(Math.Max(0, expected - assigned));
        GetAntagSelectionMetric(round, rule, type, "uncovered").Set(Math.Max(0, expected - assigned - ghostRoles));
        GetAntagSelectionMetric(round, rule, type, "forced_assignments").Set(forcedAssignments);
        GetAntagSelectionMetric(round, rule, type, "ghost_roles_created").Set(ghostRolesCreated);
        GetAntagSelectionMetric(round, rule, type, "latejoin_assignments").Inc(0);
    }

    /// <summary>
    /// Records a successful antagonist assignment made through the late-join selection path.
    /// Just here for logging, pretty much.
    /// </summary>
    private void RecordLateJoinAntagAssignment(
        Entity<AntagSelectionComponent> gameRule,
        AntagSpecifierPrototype definition)
    {
        var round = GameTicker.RoundId.ToString(CultureInfo.InvariantCulture);
        var rule = MetaData(gameRule).EntityPrototype?.ID ?? "unknown";

        GetAntagSelectionMetric(round, rule, definition.ID, "latejoin_assignments").Inc();
    }

    /// <summary>
    /// Enforces each rule's cached primary-selection target, allowing latejoins to raise it
    /// only when LateJoinAdditional is enabled. Missing slots without a ghost-role spawner are retried
    /// through normal antagonist selection, while definitions with a configured SpawnerPrototype
    /// reserve their missing slots as ghost roles.
    /// Returns true when a timed repair should be retried because an eligible live assignment
    /// or a configured ghost-role spawner failed.
    /// aka: "antags didn't roll correctly, screw it, try again"
    /// </summary>
    private bool EnforceAntagTargets(
        Entity<AntagSelectionComponent> gameRule,
        IList<ICommonSession> players)
    {
        var targets = new Dictionary<ProtoId<AntagSpecifierPrototype>,
            (AntagSpecifierPrototype Definition, int Target, int AssignedBefore, int EligibleBefore)>();
        var shortfalls = new List<AntagCount>();

        foreach (var selector in gameRule.Comp.Antags)
        {
            if (!Proto.Resolve(selector.Proto, out var definition))
                continue;

            // SelectionTargets captures the rule's primary-selection target. Latejoins may
            // only raise that target when LateJoinAdditional explicitly allows additional antags.
            // If this rule somehow missed primary selection entirely, calculate its initial target now.
            var hasCachedTarget = gameRule.Comp.SelectionTargets.TryGetValue(definition.ID, out var cachedTarget);
            var target = cachedTarget;
            if (!hasCachedTarget || gameRule.Comp.LateJoinAdditional)
            {
                var currentTarget = GetTargetAntagCount(gameRule, players.Count, definition);
                target = hasCachedTarget ? Math.Max(cachedTarget, currentTarget) : currentTarget;
            }

            gameRule.Comp.SelectionTargets[definition.ID] = target;

            var assigned = GetAssignedAntagCount(gameRule, definition.ID);
            var eligible = gameRule.Comp.SelectionTime == Never || !definition.PickPlayer
                ? 0
                : players.Count(player =>
                    CanBeAntag(player, gameRule, definition) &&
                    player.AttachedEntity is { } entity &&
                    IsSelectedProfileValidForAntag(player, entity, null, definition));
            targets[definition.ID] = (definition, target, assigned, eligible);

            if (gameRule.Comp.SelectionTime != Never &&
                definition.PickPlayer &&
                definition.SpawnerPrototype is null &&
                assigned < target)
                shortfalls.Add((definition, target - assigned));
        }

        // AssignAntag performs the existing preference, ban, job, species/profile, entity,
        // and multi-antag checks. Removing each session after one pass avoids retrying a player
        // forever while still allowing later audits to consider new or newly eligible players.
        var weightedPool = GetWeightedPlayerPool(players);
        while (shortfalls.Count > 0 && RobustRandom.TryPickAndTake(weightedPool, out var session))
            AssignAntag(gameRule, session, ref shortfalls);

        var shouldRetry = false;
        foreach (var (_, (definition, target, assignedBefore, eligibleBefore)) in targets)
        {
            var assigned = GetAssignedAntagCount(gameRule, definition.ID);
            var forcedAssignments = Math.Max(0, assigned - assignedBefore);
            var neededGhostRoles = Math.Max(0, target - assigned);
            var pendingGhostRoles = _ghostRole.GhostRoles
                .Where(role =>
                    !role.Comp.Taken &&
                    TryComp<GhostRoleAntagSpawnerComponent>(role.Owner, out var spawner) &&
                    spawner.Rule == gameRule.Owner &&
                    spawner.Definition == definition.ID)
                .ToList();

            // A live repair assignment supersedes one fallback reservation. Close only the excess
            // roles so taking an existing ghost role can never make the rule exceed its target.
            for (var i = neededGhostRoles; i < pendingGhostRoles.Count; i++)
            {
                var role = pendingGhostRoles[i];
                _ghostRole.MarkGhostRoleTaken(role);
                QueueDel(role.Owner);
            }

            var keptGhostRoles = Math.Min(neededGhostRoles, pendingGhostRoles.Count);
            var missingGhostRoles = neededGhostRoles - keptGhostRoles;
            if (missingGhostRoles > 0)
                SpawnGhostRoles(gameRule, definition, missingGhostRoles);

            var finalGhostRoles = GetPendingAntagGhostRoleCount(gameRule, definition.ID);
            var ghostRolesCreated = Math.Max(0, finalGhostRoles - keptGhostRoles);
            var unassigned = Math.Max(0, target - assigned);
            var uncovered = Math.Max(0, unassigned - finalGhostRoles);
            var message = $"{definition.ID}: target={target}, eligible={eligibleBefore}, assigned={assigned}, " +
                $"ghostRoles={finalGhostRoles}, forced={forcedAssignments}, " +
                $"ghostRolesCreated={ghostRolesCreated}, unassigned={unassigned}, uncovered={uncovered}. " +
                $"Gamerule: {ToPrettyString(gameRule)}";

            UpdateAntagSelectionMetrics(gameRule, definition, target, assigned, finalGhostRoles, forcedAssignments, ghostRolesCreated);

            // Do not keep retrying an if we literally don't have enough players who qualify.
            // If every currently eligible, opted-in player would still leave us below target,
            // ghost roles (if the antag allows them) are the final result. Retry only on a
            // failed assignment or failed spawner.
            var liveRetryPossible = definition.PickPlayer &&
                definition.SpawnerPrototype is null &&
                gameRule.Comp.SelectionTime != Never &&
                assigned < target &&
                assignedBefore + eligibleBefore >= target;
            var ghostSpawnerFailed = uncovered > 0 && definition.SpawnerPrototype is not null;
            var repairFailed = liveRetryPossible || ghostSpawnerFailed;

            // An uncovered target is expected when there are not enough eligible players and the
            // definition has no ghost-role fallback. Only report an error when a repair path that
            // should have worked actually failed.
            if (repairFailed)
                Log.Warning(message);
            else
                Log.Info(message);

            _adminLogger.Add(LogType.AntagSelection, $"{message}");
            shouldRetry |= repairFailed;
        }

        return shouldRetry;
    }

    // Antag Loadouts
        private RoleLoadout? GetSelectedLoadout(ICommonSession? session, HumanoidCharacterProfile? profile, List<ProtoId<RoleLoadoutPrototype>>? roleLoadouts, out RoleLoadoutPrototype? proto)
    {
        proto = null;

        if (profile == null || roleLoadouts == null || roleLoadouts.Count == 0)
            return null;

        foreach (var candidate in roleLoadouts)
        {
            if (!_prototypeManager.TryIndex(candidate, out proto))
                continue;

            return profile.GetLoadoutOrDefault(candidate, session, profile.Species, EntityManager, _prototypeManager).Clone();
        }

        return null;
    }

}
