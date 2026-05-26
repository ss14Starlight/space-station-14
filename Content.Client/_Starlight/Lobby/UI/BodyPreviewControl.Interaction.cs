// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System;
using Content.Shared._Starlight.Body.Editor;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    public void SelectPart(BodyPartAddress path) =>
        _store?.Dispatch(new BodyEditorSelectPartAction(path));

    public void SetHighlightSubtree(BodyPartAddress? root)
    {
        if (root is not { } r || r.IsRoot)
        {
            foreach (var (control, _) in _layerControls)
                control.Dimmed = false;
            return;
        }

        var prefix = r.Path;
        foreach (var (control, layer) in _layerControls)
        {
            var path = layer.Path.Path;
            var inSubtree = path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);
            control.Dimmed = !inSubtree;
        }
    }

    private Control CreateRotateButtons()
    {
        var rotateButtons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
        };

        var rotateLeftButton = new Button { Text = "←" };
        var rotateRightButton = new Button { Text = "→" };

        rotateLeftButton.OnPressed += _ => _store?.Dispatch(new BodyEditorRotateAction(-1));
        rotateRightButton.OnPressed += _ => _store?.Dispatch(new BodyEditorRotateAction(1));

        rotateButtons.AddChild(rotateLeftButton);
        rotateButtons.AddChild(new Control { MinSize = new Vector2(8, 0) });
        rotateButtons.AddChild(rotateRightButton);
        return rotateButtons;
    }

    private void AddPreviewLayer(PreviewLayer layer)
    {
        var control = new BodyPartPreviewControl(layer.Path, layer.LayerId.ToString(), layer.Sprite, GetTexture(layer.Sprite), GetLayerColor(layer), BodyScale, _clickMap, GetDirection(layer.Sprite), layer.Clickable)
        {
            ToolTip = GetLayerTooltip(layer),
        };
        if (layer.Clickable)
            control.Pressed += OnPreviewLayerPressed;

        var hoverPath = layer.Path;
        control.OnMouseEntered += _ => SetHighlightSubtree(hoverPath);
        control.OnMouseExited += _ => SetHighlightSubtree(null);

        _preview.AddChild(control);
        _layerControls.Add((control, layer));
    }

    private void OnPreviewLayerPressed(BodyPartAddress path, string layerId) =>
        _store?.Dispatch(new BodyEditorSelectPartAction(path));
}
