using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class VSeparator : PanelContainer
{
    private static readonly Color _separatorColor = Color.FromHex("#3D4059"); // Starlight

    // Starlight-start
    private readonly StyleBoxFlat _styleBox;

    public Color Color
    {
        get => _styleBox.BackgroundColor;
        set => _styleBox.BackgroundColor = value;
    }
    // Starlight-end

    public VSeparator(Color color, float width = 2f) // Starlight
    {
        MinSize = new Vector2(width, 5);
        VerticalExpand = true;

        // Starlight-start
        _styleBox = new StyleBoxFlat
        {
            BackgroundColor = color,
        };

        AddChild(new PanelContainer { PanelOverride = _styleBox });
        // Starlight-end
    }

    public VSeparator() : this(_separatorColor) { } // Starlight
}
