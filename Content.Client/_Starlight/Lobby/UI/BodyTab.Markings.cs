// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    private void PopulateMarkings()
    {
        SelectedMarkingsList.RemoveAllChildren();
        _selectedCards.Clear();
        SelectedMarkingsList.AddChild(BuildSelectedHeader());

        if (_store.State.SelectedPart == null || _markingManager == null || _prototype == null)
        {
            _availableMarkings.Clear();
            AvailableMarkingsGrid.SetItems(0, _ => new Control());
            return;
        }

        var available = ResolveAvailableMarkings(_store.State.SelectedPart);
        PopulateSelectedMarkings(GetAllowedCategories(available));
        PopulateAvailableMarkings(available);
    }

    private Control BuildSelectedHeader()
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        row.AddChild(new Label { Text = "Selected", StyleClasses = { "LabelHeading" } });

        var setProto = GetScopedSet();
        if (setProto == null)
            return row;

        var count = CountMarkingsInSet(setProto);
        var label = setProto.MinCount > 0
            ? $"{count}/{setProto.MaxCount} (min {setProto.MinCount})"
            : $"{count}/{setProto.MaxCount}";

        row.AddChild(new Control { HorizontalExpand = true });
        row.AddChild(new Label
        {
            Text = label,
            StyleClasses = { StyleClass.LabelSubText },
            VerticalAlignment = VAlignment.Center,
        });
        return row;
    }

    private MarkingSetPrototype? GetScopedSet()
    {
        if (_prototype == null)
            return null;
        var address = _store.State.SelectedPartPath;
        if (address is not { HasMarkingSet: true } addr || addr.MarkingSet is null)
            return null;
        return _prototype.TryIndex<MarkingSetPrototype>(addr.MarkingSet, out var proto) ? proto : null;
    }

    private int CountMarkingsInSet(MarkingSetPrototype set)
    {
        if (_store.State.SelectedPart == null)
            return 0;
        var pref = FindPreference(_store.State.BodyProfile.Root, _store.State.SelectedPart.Path);
        if (pref == null)
            return 0;
        var ids = new HashSet<string>(set.Markings.Select(m => m.Id));
        return pref.Markings.Count(m => ids.Contains(m.MarkingId));
    }

    private MarkingSetPrototype? FindOwningSet(MarkingPrototype marking)
    {
        if (_prototype == null || _store.State.SelectedPart == null)
            return null;

        var pinned = GetScopedSet();
        if (pinned != null && pinned.Markings.Any(m => m.Id == marking.ID))
            return pinned;

        foreach (var setId in _store.State.SelectedPart.MarkingSets)
        {
            if (_prototype.TryIndex<MarkingSetPrototype>(setId, out var proto)
                && proto.Markings.Any(m => m.Id == marking.ID))
                return proto;
        }
        return null;
    }

    private List<MarkingPrototype> ResolveAvailableMarkings(BodyEditorBodyPartState part)
    {
        var list = new List<MarkingPrototype>();
        if (_markingManager == null || _prototype == null)
            return list;

        var setIds = GetSelectedMarkingSetIds(part);
        if (setIds.Count == 0)
            return list;

        var allowedMarkings = new HashSet<string>();
        foreach (var setId in setIds)
        {
            if (!_prototype.TryIndex<MarkingSetPrototype>(setId, out var setProto))
                continue;
            foreach (var marking in setProto.Markings)
                allowedMarkings.Add(marking.Id);
        }

        foreach (var id in allowedMarkings)
        {
            if (_markingManager.Markings.TryGetValue(id, out var marking))
                list.Add(marking);
        }
        return list;
    }

    private HashSet<string> GetSelectedMarkingSetIds(BodyEditorBodyPartState part)
    {
        var setIds = new HashSet<string>();
        var address = _store.State.SelectedPartPath ?? part.Path;
        if (address.HasMarkingSet)
        {
            setIds.Add(address.MarkingSet!);
            return setIds;
        }

        foreach (var setId in part.MarkingSets)
            setIds.Add(setId.Id);
        return setIds;
    }

    private static HashSet<MarkingCategories> GetAllowedCategories(List<MarkingPrototype> markings)
        => markings.Select(marking => marking.MarkingCategory).ToHashSet();

    private void PopulateSelectedMarkings(HashSet<MarkingCategories> categories)
    {
        if (!_store.State.Character.HasProfile || _store.State.SelectedPart == null || _markingManager == null)
            return;

        var pref = FindPreference(_store.State.BodyProfile.Root, _store.State.SelectedPart.Path);
        if (pref == null)
            return;

        var itemSize = GetSelectedMarkingCardSize();

        foreach (var marking in pref.Markings)
        {
            if (!_markingManager.TryGetMarking(marking, out var prototype) || !categories.Contains(prototype.MarkingCategory))
                continue;

            SelectedMarkingsList.AddChild(CreateSelectedMarkingCard(prototype, marking.MarkingColors.ToList(), marking.MarkingId, itemSize));
        }
    }

    private Vector2 GetSelectedMarkingCardSize()
    {
        var width = SelectedMarkingsList.Width - 12f;
        if (width <= 0)
            width = SelectedMarkingsList.MinWidth;
        if (width <= 0)
            width = AvailableMarkingsGrid.ItemSize.X;

        return new Vector2(width, width);
    }

    private void PopulateAvailableMarkings(List<MarkingPrototype> markings)
    {
        _availableMarkings.Clear();

        if (!_store.State.Character.HasProfile || _markingManager == null)
        {
            AvailableMarkingsGrid.SetItems(0, _ => new Control());
            return;
        }

        markings.Sort((a, b) => string.Compare(GetMarkingName(a), GetMarkingName(b), StringComparison.Ordinal));
        _availableMarkings.AddRange(markings);
        AvailableMarkingsGrid.SetItems(_availableMarkings.Count, BuildAvailableMarkingCard);
    }

    private Control BuildAvailableMarkingCard(int index)
    {
        var prototype = _availableMarkings[index];
        var isSelected = GetSelectedMarkingIds().Contains(prototype.ID);
        return CreateMarkingCard(prototype, isSelected, () => AddMarking(prototype));
    }

    private static string GetMarkingName(MarkingPrototype marking)
        => Loc.GetString($"marking-{marking.ID}");
}
