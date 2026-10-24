using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Components;

[ByRefEvent]
public record struct TakeGhostRoleEvent(ICommonSession Player)
{
    public bool TookRole { get; set; }
}

// Starlight-start
/// <summary>Checks a player's eligibility before listing a ghost role or accepting their request.</summary>
[ByRefEvent]
public record struct GhostRoleRequestAttemptEvent(ICommonSession Player)
{
    public bool Cancelled;
}
// Starlight-end
