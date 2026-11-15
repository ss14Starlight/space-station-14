using Content.Server.GameTicking.Events;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.GameTicking.Commands;

sealed partial class JoinGameCommand
{
    private bool IsJobAllowed(ICommonSession player, ProtoId<JobPrototype> job)
    {
        if (_banManager.GetJobBans(player.UserId) is not { } jobBans || jobBans.Contains(job))
            return false;

        var allowedEv = new IsRoleAllowedEvent(player, new List<ProtoId<JobPrototype>> { job }, null, isSpawning: false);
        _entManager.EventBus.RaiseEvent(EventSource.Local, ref allowedEv);
        return !allowedEv.Cancelled;
    }
}
