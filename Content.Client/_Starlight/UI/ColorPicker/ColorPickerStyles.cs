// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.UI;

internal static class ColorPickerStyles
{
    public static readonly Color DefaultBorder = new(0, 0, 0, 0.5f);

    public static StyleBoxFlat Border(Color? border = null, int thickness = 1) => new()
    {
        BackgroundColor = Color.Transparent,
        BorderColor = border ?? DefaultBorder,
        BorderThickness = new Thickness(thickness),
    };

    public static StyleBoxFlat Filled(Color bg, Color? border = null, int thickness = 1) => new()
    {
        BackgroundColor = bg,
        BorderColor = border ?? DefaultBorder,
        BorderThickness = new Thickness(thickness),
    };

    public static PanelContainer BorderOverlay(Control parent)
    {
        var border = new PanelContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
            PanelOverride = Border(),
        };
        LayoutContainer.SetAnchorPreset(border, LayoutContainer.LayoutPreset.Wide);
        parent.AddChild(border);
        return border;
    }
}
