// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

internal sealed class RemoveMarkingButton : PanelContainer
{
    private static readonly Color _borderIdle = Color.FromHex("#A88B5E66");
    private static readonly Color _borderHover = Color.FromHex("#ff6b6b");
    private static readonly Color _backgroundIdle = new(1f, 1f, 1f, 0.04f);
    private static readonly Color _backgroundHover = new(1f, 0.2f, 0.2f, 0.12f);

    private readonly StyleBoxFlat _style;
    private readonly Action _onPressed;

    public RemoveMarkingButton(Action onPressed)
    {
        _onPressed = onPressed;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(16, 16);
        SetSize = new Vector2(16, 16);
        VerticalAlignment = VAlignment.Center;
        _style = new StyleBoxFlat
        {
            BackgroundColor = _backgroundIdle,
            BorderColor = _borderIdle,
            BorderThickness = new Thickness(1),
        };
        PanelOverride = _style;

        AddChild(new Label
        {
            Text = "X",
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SetWidth = 16,
            SetHeight = 16,
            MouseFilter = MouseFilterMode.Ignore,
        });
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _onPressed();
        args.Handle();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _style.BackgroundColor = _backgroundHover;
        _style.BorderColor = _borderHover;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _style.BackgroundColor = _backgroundIdle;
        _style.BorderColor = _borderIdle;
    }
}
