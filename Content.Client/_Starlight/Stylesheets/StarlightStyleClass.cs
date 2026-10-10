namespace Content.Client._Starlight.Stylesheets;

/// <summary>
/// Style classes added by the themed sheet.
/// </summary>
public static class StarlightStyleClass
{
    /// <summary>
    /// A bordered surface for lists and logs, so they stand apart from the window body.
    /// </summary>
    public const string InsetPanel = "InsetPanel";

    /// <summary>
    /// A style class for a preview of a sprite, which is a small image or icon that represents an object or character in the game.
    /// </summary>
    public const string SpritePreview = "SpritePreview";

    /// <summary>
    /// A style class for a lobby card, which is a rectangular panel that displays information about a player or a game session in the lobby.
    /// </summary>
    public const string LobbyCard = "LobbyCard";

    public const string LobbyTopBar = "LobbyTopBar";

    /// <summary>
    /// A style class for the ready button in the lobby, which is a button that players can click to indicate that they are ready to start the game.
    /// </summary>
    public const string LobbyReadyButton = "LobbyReadyButton";

    /// <summary>
    /// A style class for the buttons in the lobby bar, which are buttons that allow players to perform various actions in the lobby, such as changing settings or leaving the game.
    /// </summary>
    public const string LobbyBarButton = "LobbyBarButton";

    /// <summary>
    /// A style class for the action buttons in the lobby, which are buttons that allow players to perform specific actions related to the game session, such as joining a team or selecting a role.
    /// </summary>
    public const string LobbyActionButton = "LobbyActionButton";

    /// <summary>
    /// A style class for the hub panel, which is a large container that holds various elements of the hub interface, such as job cards, priority bars, and other controls.
    /// </summary>
    public const string HubPanel = "HubPanel";

    public const string HubRow = "HubRow";

    public const string JobCard = "JobCard";

    /// <summary>
    /// A style class for a job card that is highlighted, which is a rectangular panel that displays information about a job or role in the game, and is visually emphasized to indicate its importance or urgency.
    /// </summary>
    public const string JobCardHigh = "JobCardHigh";

    public const string PriorityBar = "PriorityBar";
    public const string PriorityBarFilled = "PriorityBarFilled";

    /// <summary>
    /// A style class for a job drop highlight, which is a visual effect that indicates that a job or role has been dropped or removed from the selection, and is used to draw attention to the change in the job list.
    /// </summary>
    public const string JobDropHighlight = "JobDropHighlight";
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
