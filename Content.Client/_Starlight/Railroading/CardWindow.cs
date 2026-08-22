using System.Numerics;
using Content.Client._Starlight.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Railroading;

internal static class CardWindow
{
    private static readonly Vector2 _emptySize = new(320, 96);

    /// <summary>
    /// Fills a window with the placeholder shown when there is nothing to pick.
    /// </summary>
    internal static void RenderEmpty(SLWindow window)
    {
        window.Resizable = false;
        window.Contents.SetSize = _emptySize;
        window.Contents.MinSize = _emptySize;
        window.Contents.MaxSize = _emptySize;

        window.Title = Loc.GetString("card-selection-window-title");

        window.Contents.RemoveAllChildren();
        window.Box(BoxContainer.LayoutOrientation.Vertical, box =>
        {
            box.Align = BoxContainer.AlignMode.Center;
            box.HorizontalExpand = true;
            box.VerticalExpand = true;
            box.Label(label =>
            {
                label.WithText(Loc.GetString("card-selection-no-cards"));
                label.HorizontalAlignment = Control.HAlignment.Center;
                label.VerticalAlignment = Control.VAlignment.Center;
            });
        });
    }
}
