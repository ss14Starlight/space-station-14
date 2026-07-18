using Robust.Shared.Serialization;

namespace Content.Shared._Starlight;

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
}
