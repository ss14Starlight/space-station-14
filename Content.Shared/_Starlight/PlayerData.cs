using Content.Shared.Administration;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight;

/// <summary>
///     Represents data for a single player.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerData
{
    /// <summary>
    ///     The player's title.
    /// </summary>
    public string? Title;
    
    public string? GhostTheme;
    
    public Color GhostThemeColor = Color.White;

    [Obsolete("Use ISharedNullLinkPlayerResourcesManager to access this field")]
    public Dictionary<string, double> Resources = [];
}