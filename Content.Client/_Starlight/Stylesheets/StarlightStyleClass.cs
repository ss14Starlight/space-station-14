namespace Content.Client._Starlight.Stylesheets;

/// <summary>
/// Style classes added by the themed sheet.
/// </summary>
public static class StarlightStyleClass
{
    #region General

    /// <summary>
    /// A bordered surface for lists and logs, so they stand apart from the window body.
    /// </summary>
    public const string InsetPanel = "InsetPanel";

    /// <summary>
    /// A framed backdrop behind a sprite preview.
    /// </summary>
    public const string SpritePreview = "SpritePreview";

    /// <summary>
    /// A window header button that collapses or expands the window's sidebar. The window sets its texture.
    /// </summary>
    public const string WindowSidebarButton = "WindowSidebarButton";

    #endregion

    #region Lobby

    /// <summary>
    /// A translucent panel over the lobby art, such as the character card or the song and art info.
    /// </summary>
    public const string LobbyCard = "LobbyCard";

    /// <summary>
    /// The bar along the top of the lobby with the server name and the menu buttons.
    /// </summary>
    public const string LobbyTopBar = "LobbyTopBar";

    /// <summary>
    /// A flat button on <see cref="LobbyTopBar"/>.
    /// </summary>
    public const string LobbyBarButton = "LobbyBarButton";

    /// <summary>
    /// The big ready button, filled with the accent color.
    /// </summary>
    public const string LobbyReadyButton = "LobbyReadyButton";

    /// <summary>
    /// A big bordered button next to <see cref="LobbyReadyButton"/>, such as observe.
    /// </summary>
    public const string LobbyActionButton = "LobbyActionButton";

    #endregion

    #region Server hub

    /// <summary>
    /// A panel with an accent edge on the left, for the server list and the ready block.
    /// </summary>
    public const string HubPanel = "HubPanel";

    /// <summary>
    /// A row inside <see cref="HubPanel"/>.
    /// </summary>
    public const string HubRow = "HubRow";

    #endregion

    #region Job priorities

    /// <summary>
    /// A clickable card of a job in the job priorities window.
    /// </summary>
    public const string JobCard = "JobCard";

    /// <summary>
    /// A <see cref="JobCard"/> of the high priority job, bordered with the accent color.
    /// </summary>
    public const string JobCardHigh = "JobCardHigh";

    /// <summary>
    /// The priority marker of a job that is never picked.
    /// </summary>
    public const string PriorityBar = "PriorityBar";

    /// <summary>
    /// Color markers of a job priority: high is green, medium yellow, low red.
    /// </summary>
    public const string PriorityHigh = "PriorityHigh";
    public const string PriorityMedium = "PriorityMedium";
    public const string PriorityLow = "PriorityLow";

    /// <summary>
    /// Highlights the column a job is dragged over in the drag and drop priority columns.
    /// </summary>
    public const string JobDropHighlight = "JobDropHighlight";

    #endregion
}

/// <summary>
/// Style properties added by the themed sheet.
/// </summary>
public static class StarlightStyleProperty
{
    /// <summary>
    /// The color of an icon, used to tint the icon's texture. This is useful for changing the color of icons without needing to create multiple textures for each color.
    /// </summary>
    public const string IconColor = "icon-color";
}
