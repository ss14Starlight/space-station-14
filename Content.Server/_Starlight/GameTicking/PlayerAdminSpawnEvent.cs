using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server._Starlight.GameTicking;

/// <summary>
/// Raised when a player is spawned via the "Spawn here" admin verb.
/// </summary>
public sealed class PlayerAdminSpawnEvent(
    EntityUid mob,
    ICommonSession player,
    string? jobId,
    EntityUid? station,
    HumanoidCharacterProfile profile)
    : EntityEventArgs
{
    public EntityUid Mob { get; } = mob;
    public ICommonSession Player { get; } = player;
    public string? JobId { get; } = jobId;
    public EntityUid? Station { get; } = station;
    public HumanoidCharacterProfile Profile { get; } = profile;
}