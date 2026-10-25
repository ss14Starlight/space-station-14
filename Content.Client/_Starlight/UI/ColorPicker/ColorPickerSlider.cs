// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Starlight.UI;

internal abstract class ColorPickerSlider : LayoutContainer
{
    protected readonly StarlightColorPicker Owner;
    private bool _grabbed;

    protected ColorPickerSlider(StarlightColorPicker owner)
    {
        Owner = owner;
        MouseFilter = MouseFilterMode.Stop;
    }

    protected abstract void HandleAt(Vector2 relative);

    protected override void Resized()
    {
        base.Resized();
        Owner.SyncMarkers();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _grabbed = true;
        HandleAt(args.RelativePosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_grabbed)
            Owner.CommitToRecent();
        _grabbed = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        if (_grabbed)
            HandleAt(args.RelativePosition);
    }
}
