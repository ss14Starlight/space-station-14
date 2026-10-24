using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Components;

[ByRefEvent]
public record struct TakeGhostRoleEvent(ICommonSession Player)
{
    public bool TookRole { get; set; }
}

// Starlight-start
/// <summary>
/// Raised on a ghost role to validate a player's eligibility before listing it or accepting a request.
/// </summary>
[ByRefEvent]
public record struct GhostRoleRequestAttemptEvent(ICommonSession Player)
{
    public bool Cancelled;
}
// Starlight-end
