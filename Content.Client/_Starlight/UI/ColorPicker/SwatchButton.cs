// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Starlight.UI;

internal sealed class SwatchButton : PanelContainer
{
    private readonly Action? _onClick;

    public SwatchButton(StarlightColorPicker owner, int slot, Color? color, bool allowed, bool isActive)
    {
        MinSize = new Vector2(18, 18);
        var disabled = color is not null && !allowed;
        MouseFilter = disabled ? MouseFilterMode.Ignore : MouseFilterMode.Stop;

        var bg = color ?? new Color(0.15f, 0.15f, 0.15f);
        var displayed = color is null ? bg
            : allowed ? bg : new Color(bg.R * 0.35f, bg.G * 0.35f, bg.B * 0.35f, 0.6f);

        PanelOverride = ColorPickerStyles.Filled(
            displayed,
            isActive ? Color.White : new Color(0, 0, 0, 0.6f),
            thickness: isActive ? 2 : 1);

        ToolTip = color is null
            ? Loc.GetString("starlight-color-picker-empty-slot")
            : allowed
                ? color.Value.ToHexNoAlpha()
                : Loc.GetString("starlight-color-picker-unavailable", ("hex", color.Value.ToHexNoAlpha()));

        if (!disabled)
            _onClick = () => owner.SelectSlot(slot, color);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || _onClick == null)
            return;

        _onClick();
        args.Handle();
    }
}
