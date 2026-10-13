namespace Content.Client.Pinpointer.UI;

public partial class NavMapControl
{
    /// <summary>
    /// Subclasses in resizable windows can opt out of the standard fixed-width map header.
    /// </summary>
    protected virtual float TopPanelWidth => 650f;
}
