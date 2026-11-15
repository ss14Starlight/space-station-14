using Content.Server.Players.JobWhitelist;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.GameTicking.Commands;

sealed partial class JoinGameCommand
{
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;

    private bool IsJobAllowed(ICommonSession player, ProtoId<JobPrototype> job)
    {
        if (_banManager.GetJobBans(player.UserId) is not { } jobBans || jobBans.Contains(job))
            return false;

        return _jobWhitelist.IsAllowed(player, job);
    }
}
