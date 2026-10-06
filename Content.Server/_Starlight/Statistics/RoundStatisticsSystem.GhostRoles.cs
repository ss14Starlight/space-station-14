using Content.Server.Ghost.Roles.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles.Components;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    private readonly Dictionary<GhostRoleKey, GhostRoleAccumulator> _ghostRoles = [];

    private void InitializeGhostRoleStatistics()
        => RegisterStatisticsDomain(EmitGhostRoleStatistics, ClearGhostRoleStatistics);

    /// <summary>
    /// Records a ghost role becoming available, such as the slots that arrive with a dispatched ERT.
    /// </summary>
    public void RecordGhostRoleOffered(Entity<GhostRoleComponent> role)
    {
        if (!EnsureRound())
            return;

        var statistic = GetOrAddGhostRole(role);
        statistic.Offered++;
        statistic.Slots += CompOrNull<GhostRoleMobSpawnerComponent>(role)?.AvailableTakeovers ?? 1;
    }

    /// <summary>
    /// Records a player taking a ghost role.
    /// </summary>
    public void RecordGhostRoleTaken(Entity<GhostRoleComponent> role)
    {
        if (!EnsureRound())
            return;

        GetOrAddGhostRole(role).Taken++;
    }

    private void EmitGhostRoleStatistics(RoundEndMessageEvent args)
    {
        var offered = 0;
        var slots = 0;
        var taken = 0;

        foreach (var (key, statistic) in _ghostRoles)
        {
            offered += statistic.Offered;
            slots += statistic.Slots;
            taken += statistic.Taken;

            EmitRoundRecord(
                args.RoundId,
                "kind=ghost_role role_id={RoleId} job_id={JobId} offered={Offered} slots={Slots} taken={Taken}",
                key.Role,
                key.Job,
                statistic.Offered,
                statistic.Slots,
                statistic.Taken);
        }

        EmitRoundRecord(
            args.RoundId,
            "kind=ghost_role_summary roles={Roles} offered={Offered} slots={Slots} taken={Taken}",
            _ghostRoles.Count,
            offered,
            slots,
            taken);
    }

    private void ClearGhostRoleStatistics()
        => _ghostRoles.Clear();

    /// <summary>
    /// A ghost role has no prototype of its own and its display name is localized, so it is keyed by
    /// what it spawns and the job it hands out. Every ERT, CBURN and death squad role is a randomly
    /// generated humanoid, so there only the job tells them apart.
    /// </summary>
    private GhostRoleAccumulator GetOrAddGhostRole(Entity<GhostRoleComponent> role)
    {
        var spawned = CompOrNull<GhostRoleMobSpawnerComponent>(role)?.Prototype?.Id;
        var key = new GhostRoleKey(
            spawned ?? MetaData(role).EntityPrototype?.ID ?? "unknown",
            role.Comp.JobProto?.Id ?? "none");

        if (_ghostRoles.TryGetValue(key, out var statistic))
            return statistic;

        statistic = new GhostRoleAccumulator();
        _ghostRoles.Add(key, statistic);
        return statistic;
    }

    private readonly record struct GhostRoleKey(string Role, string Job);

    private sealed class GhostRoleAccumulator
    {
        public int Offered;
        public int Slots;
        public int Taken;
    }
}
