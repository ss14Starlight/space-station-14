// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using System.Numerics;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    private Control CreateMarkingCard(MarkingPrototype prototype, bool isSelected, Action onPressed)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            Text = GetMarkingName(prototype),
            ToolTip = GetMarkingName(prototype),
        };
        button.OnPressed += _ => onPressed();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        container.AddChild(BuildMarkingSpritesPreview(prototype, prototype.AsMarking().MarkingColors.ToList(), AvailableMarkingsGrid.ItemSize));
        container.AddChild(button);
        return container;
    }

    private Control CreateSelectedMarkingCard(MarkingPrototype prototype, List<Color> colors, string markingId)
    {
        var card = new SelectedMarkingCard(markingId, this, () => BuildSelectedPreviewControl(prototype, colors));
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var previewHost = new LayoutContainer
        {
            MinSize = new Vector2(48, 48),
        };
        var preview = BuildSelectedPreviewControl(prototype, colors);
        LayoutContainer.SetAnchorPreset(preview, LayoutContainer.LayoutPreset.Wide);
        previewHost.AddChild(preview);
        card.SetPreviewHost(previewHost, preview);

        var label = new Label
        {
            Text = GetMarkingName(prototype),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
        };

        var remove = new Button
        {
            Text = "X",
            VerticalAlignment = VAlignment.Center,
        };
        remove.OnPressed += _ => RemoveMarking(markingId);

        container.AddChild(previewHost);
        container.AddChild(label);
        container.AddChild(remove);
        card.AddChild(container);
        _selectedCards[markingId] = card;
        return card;
    }

    private HashSet<string> GetSelectedMarkingIds()
    {
        if (_store.State.SelectedPart == null)
            return new HashSet<string>();

        var pref = FindPreference(_store.State.BodyProfile.Root, _store.State.SelectedPart.Path);
        return pref?.Markings.Select(m => m.MarkingId).ToHashSet() ?? new HashSet<string>();
    }
}
