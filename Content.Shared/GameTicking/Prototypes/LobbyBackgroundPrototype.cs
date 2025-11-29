//starlight, file moved from server to shared

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.GameTicking.Prototypes;

/// <summary>
/// Prototype for a lobby background the game can choose.
/// </summary>
[Prototype]
public sealed partial class LobbyBackgroundPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// The sprite to use as the background. This should ideally be 1920x1080.
    /// </summary>
    [DataField("background", required: true)]
    public ResPath Background = default!;

    //starlight start
    [DataField("title")]
    public string? Title;

    [DataField("artist")]
    public string? Artist;

    /// <summary>
    /// If true, this background will not be shown in the lobby background selection menu.
    /// Useful for gamemode forced backgrounds.
    /// </summary>
    [DataField("excludeFromMenu")]
    public bool ExcludeFromMenu = false;
    //starlight end
}
