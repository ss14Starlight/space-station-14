// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Client.Stylesheets;
using Content.Shared._Starlight.Body.Editor;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    private static readonly Color _flatBgColor = Color.FromHex("#FFFFFF0A");
    private static readonly Color _flatBgActiveColor = Color.FromHex("#A88B5E59");

    private void PopulateBodyParts()
    {
        BodyPartsList.RemoveAllChildren();

        var root = _store.State.Character.BodyRoot;
        if (root == null)
            return;

        var selectedAddress = _store.State.SelectedPartPath ?? root.Path;
        var selectedPart = _store.State.SelectedPart ?? root;
        var hasMarkingSetScope = selectedAddress.HasMarkingSet;

        var crumbs = new List<BodyEditorBodyPartState>();
        BuildBreadcrumb(root, selectedPart.Path, crumbs);

        var breadcrumbRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
        };

        for (var i = 0; i < crumbs.Count; i++)
        {
            var crumb = crumbs[i];
            var parent = i > 0 ? crumbs[i - 1] : null;
            var isCurrent = !hasMarkingSetScope && crumb == selectedPart;
            var button = CreateFlatNavButton(GetPartName(crumb, parent), isCurrent, breadcrumb: true);
            var capture = crumb.Path;
            button.OnPressed += _ => _store.Dispatch(new BodyEditorSelectPartAction(capture));
            HookHover(button, capture);
            breadcrumbRow.AddChild(button);

            if (i < crumbs.Count - 1)
                breadcrumbRow.AddChild(new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(1f, 1f, 1f, 0.15f) },
                    MinSize = new Vector2(1, 0),
                    VerticalExpand = true,
                });
        }

        BodyPartsList.AddChild(breadcrumbRow);

        foreach (var child in selectedPart.Children)
        {
            var button = CreateFlatNavButton("▸  " + GetPartName(child, selectedPart), pressed: false);
            var capture = child.Path;
            button.OnPressed += _ => _store.Dispatch(new BodyEditorSelectPartAction(capture));
            HookHover(button, capture);
            BodyPartsList.AddChild(button);
        }

        foreach (var setId in selectedPart.MarkingSets)
        {
            var scoped = selectedPart.Path.WithMarkingSet(setId.Id);
            var isCurrent = hasMarkingSetScope && selectedAddress == scoped;
            var button = CreateFlatNavButton("✦  " + setId.Id, isCurrent);
            button.OnPressed += _ => _store.Dispatch(new BodyEditorSelectPartAction(scoped));
            HookHover(button, selectedPart.Path);
            BodyPartsList.AddChild(button);
        }
    }

    private static Button CreateFlatNavButton(string text, bool pressed, bool breadcrumb = false)
    {
        var bg = new StyleBoxFlat
        {
            BackgroundColor = pressed ? _flatBgActiveColor : _flatBgColor,
            ContentMarginLeftOverride = breadcrumb ? 6 : 4,
            ContentMarginRightOverride = breadcrumb ? 6 : 4,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };

        return new Button
        {
            Text = text,
            StyleBoxOverride = bg,
            StyleClasses = { StyleClass.ButtonSmall },
            ToggleMode = true,
            Pressed = pressed,
            HorizontalExpand = !breadcrumb,
            TextAlign = breadcrumb ? Label.AlignMode.Center : Label.AlignMode.Left,
        };
    }

    private void HookHover(Control control, BodyPartAddress path)
    {
        control.OnMouseEntered += _ => BodyPreview.SetHighlightSubtree(path);
        control.OnMouseExited += _ => BodyPreview.SetHighlightSubtree(null);
    }

    private static void BuildBreadcrumb(BodyEditorBodyPartState root, BodyPartAddress target, List<BodyEditorBodyPartState> output)
    {
        output.Add(root);
        var node = root;
        foreach (var segment in target.Segments)
        {
            if (segment == "root")
                continue;
            BodyEditorBodyPartState? next = null;
            foreach (var child in node.Children)
            {
                if (child.SlotId == segment)
                {
                    next = child;
                    break;
                }
            }
            if (next == null)
                return;
            output.Add(next);
            node = next;
        }
    }

    private static string GetPartName(BodyEditorBodyPartState part, BodyEditorBodyPartState? parent = null)
    {
        if (string.IsNullOrEmpty(parent?.SlotId))
            return Loc.GetString("body-editor-part-torso");

        var slot = part.SlotId;
        if (slot is "Hand" or "Foot")
        {
            return parent.SlotId switch
            {
                "LeftArm" => Loc.GetString("body-editor-part-left-hand"),
                "RightArm" => Loc.GetString("body-editor-part-right-hand"),
                "LeftLeg" => Loc.GetString("body-editor-part-left-foot"),
                "RightLeg" => Loc.GetString("body-editor-part-right-foot"),
                _ => slot,
            };
        }

        return slot switch
        {
            "Head" => Loc.GetString("body-editor-part-head"),
            "LeftArm" => Loc.GetString("body-editor-part-left-arm"),
            "RightArm" => Loc.GetString("body-editor-part-right-arm"),
            "LeftLeg" => Loc.GetString("body-editor-part-left-leg"),
            "RightLeg" => Loc.GetString("body-editor-part-right-leg"),
            _ => slot,
        };
    }
}
