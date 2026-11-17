namespace Content.Client.UserInterface.Controls.FancyTree;

/// <summary>
///     Visibility checks that keep tree connectors from extending into filtered-out rows.
/// </summary>
public sealed partial class TreeLine
{
    /// <summary>
    ///     Finds the last visible child so connector endpoints never use a hidden row's cached position.
    /// </summary>
    private static TreeItem? GetLastVisibleChild(TreeItem parent)
    {
        TreeItem? lastItem = null;
        foreach (var child in parent.Body.Children)
        {
            if (child is TreeItem { Visible: true } item)
                lastItem = item;
        }

        return lastItem;
    }
}
