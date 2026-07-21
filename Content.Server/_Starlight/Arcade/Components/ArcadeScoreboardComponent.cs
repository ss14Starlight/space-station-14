using Content.Server._Starlight.Arcade.Prototypes;
using Content.Shared._Starlight.Arcade.Systems;
using Content.Shared._Starlight.CCVar;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Arcade.Components;

/// <summary>
/// Arcade machines with this component have three kinds of scoreboards: machine, local, and global.
/// Machine scoreboards are per-entity; local scoreboards are per-session; global scoreboards are all-time.
/// </summary>
[RegisterComponent]
public sealed partial class ArcadeScoreboardComponent : Component
{
    /// <summary>
    /// The ID of the "server" scoreboard used for both local and global scoreboards.
    /// </summary>
    [DataField]
    public ProtoId<ArcadeScoreboardPrototype>? ServerScoreboard = null;

    /// <summary>
    /// The maximum amount of top scores to keep track of for this scoreboard.
    /// </summary>
    /// <remarks>
    /// Will use the fallback count in <see cref="ServerScoreboard"/>, then
    /// <see cref="StarlightCCVars.FallbackScoreboardEntriesCount"/>, if this is null.
    /// </remarks>
    [DataField("maxEntries")]
    public int? MaxEntriesOverride = null;

    /// <summary>
    /// A list of all score entries for this scoreboard.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public List<ArcadeHighScoreEntry> Scoreboard = new();
}
