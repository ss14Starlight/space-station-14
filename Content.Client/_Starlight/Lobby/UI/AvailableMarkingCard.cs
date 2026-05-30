// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Starlight.Lobby.UI;

internal sealed class AvailableMarkingCard : PanelContainer
{
    private static readonly Color BorderIdle = Color.FromHex("#A88B5E66");
    private static readonly Color BorderHover = Color.FromHex("#A88B5E");
    private static readonly Color BorderSelected = Color.FromHex("#ffd24f");
    private static readonly Color BackgroundIdle = new(1f, 1f, 1f, 0.04f);
    private static readonly Color BackgroundHover = new(1f, 1f, 1f, 0.08f);

    private readonly StyleBoxFlat _style;
    private readonly Action _onPressed;
    private readonly bool _selected;

    public AvailableMarkingCard(bool selected, Action onPressed)
    {
        _selected = selected;
        _onPressed = onPressed;
        MouseFilter = MouseFilterMode.Stop;
        _style = new StyleBoxFlat
        {
            BackgroundColor = BackgroundIdle,
            BorderColor = selected ? BorderSelected : BorderIdle,
            BorderThickness = new Thickness(1),
        };
        PanelOverride = _style;
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
        _style.BackgroundColor = BackgroundHover;
        _style.BorderColor = _selected ? BorderSelected : BorderHover;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _style.BackgroundColor = BackgroundIdle;
        _style.BorderColor = _selected ? BorderSelected : BorderIdle;
    }
}
