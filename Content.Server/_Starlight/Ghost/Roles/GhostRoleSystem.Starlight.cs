using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    private bool CanRequestGhostRole(EntityUid role, ICommonSession player)
    {
        var attempt = new GhostRoleRequestAttemptEvent(player);
        RaiseLocalEvent(role, ref attempt);
        return !attempt.Cancelled;
    }
}
