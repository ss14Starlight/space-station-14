using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Players.PlayTimeTracking;

public sealed partial class PlayTimeTrackingSystem
{
    /// <summary>
    /// Checks the job requirements (playtime and profile ones such as species or traits) against one specific profile,
    /// unlike <see cref="IsAllowed(ICommonSession, ProtoId{JobPrototype})"/>, which passes if any enabled profile qualifies.
    /// </summary>
    public bool IsProfileAllowedForJob(ICommonSession player, ProtoId<JobPrototype> job, HumanoidCharacterProfile profile)
        => JobRequirements.TryRequirementsMet(
            _roles.GetRoleRequirements(job),
            player,
            GetPlayTimesIfEnabled(player),
            out _,
            EntityManager,
            _prototypes,
            profile);
}
