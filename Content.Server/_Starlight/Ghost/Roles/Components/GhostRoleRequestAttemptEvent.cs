using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Ghost.Roles.Components;

/// <summary>Checks a player's eligibility before listing a ghost role or accepting their request.</summary>
[ByRefEvent]
public record struct GhostRoleRequestAttemptEvent(ICommonSession Player)
{
    public bool Cancelled;
}
