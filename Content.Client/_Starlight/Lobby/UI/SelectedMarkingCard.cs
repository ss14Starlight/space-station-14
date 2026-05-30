// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Starlight.Lobby.UI;

internal sealed class SelectedMarkingCard : PanelContainer
{
    private static readonly Color _borderIdle = Color.FromHex("#A88B5E");
    private static readonly Color _borderDragged = Color.FromHex("#ffd24f");
    private static readonly Color _borderDropTarget = Color.FromHex("#4fa3ff");

    private readonly string _markingId;
    private readonly BodyTab _owner;
    private readonly StyleBoxFlat _style;
    private readonly Func<Control> _previewFactory;
    private LayoutContainer? _previewHost;
    private Control? _preview;

    public SelectedMarkingCard(string markingId, BodyTab owner, Func<Control> previewFactory)
    {
        _markingId = markingId;
        _owner = owner;
        _previewFactory = previewFactory;
        MouseFilter = MouseFilterMode.Stop;
        _style = new StyleBoxFlat
        {
            BackgroundColor = new Color(1f, 1f, 1f, 0.04f),
            BorderColor = _borderIdle,
            BorderThickness = new Thickness(1),
        };
        PanelOverride = _style;
    }

    public void SetPreviewHost(LayoutContainer host, Control preview)
    {
        _previewHost = host;
        _preview = preview;
    }

    public void RebuildPreview()
    {
        if (_previewHost == null || _preview == null)
            return;

        var index = _preview.GetPositionInParent();
        _previewHost.RemoveChild(_preview);

        var next = _previewFactory();
        LayoutContainer.SetAnchorPreset(next, LayoutContainer.LayoutPreset.Wide);
        _previewHost.AddChild(next);
        next.SetPositionInParent(index);

        _preview = next;
    }

    public void SetDragState(bool isDragged, bool isDropTarget)
    {
        Modulate = isDragged ? new Color(1f, 1f, 1f, 0.5f) : Color.White;

        if (isDropTarget)
        {
            _style.BorderColor = _borderDropTarget;
            _style.BorderThickness = new Thickness(2);
        }
        else if (isDragged)
        {
            _style.BorderColor = _borderDragged;
            _style.BorderThickness = new Thickness(2);
        }
        else
        {
            _style.BorderColor = _borderIdle;
            _style.BorderThickness = new Thickness(1);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _owner.BeginMarkingDrag(_markingId);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _owner.EndMarkingDrag();
        args.Handle();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _owner.UpdateMarkingDropTarget(_markingId);
    }
}
