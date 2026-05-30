// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Preferences;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    internal void BeginMarkingDrag(string markingId)
    {
        _draggedMarkingId = markingId;
        _dropTargetMarkingId = markingId;
        RefreshDragVisuals();
    }

    internal void UpdateMarkingDropTarget(string markingId)
    {
        if (_draggedMarkingId == null)
            return;

        _dropTargetMarkingId = markingId;
        RefreshDragVisuals();
    }

    internal void EndMarkingDrag()
    {
        var dragged = _draggedMarkingId;
        var target = _dropTargetMarkingId;
        _draggedMarkingId = null;
        _dropTargetMarkingId = null;
        RefreshDragVisuals();

        if (dragged == null || target == null || dragged == target || _store.State.SelectedPart == null)
            return;

        var partPath = _store.State.SelectedPart.Path;
        var pref = FindPreference(_store.State.BodyProfile.Root, partPath);
        if (pref == null)
            return;

        var fromIndex = pref.Markings.FindIndex(m => m.MarkingId == dragged);
        var toIndex = pref.Markings.FindIndex(m => m.MarkingId == target);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            return;

        _store.MutateBodyProfile(p =>
        {
            var node = FindPreference(p.Root, partPath)!;
            var item = node.Markings[fromIndex];
            node.Markings.RemoveAt(fromIndex);
            node.Markings.Insert(toIndex, item);
        }, BodyEditorChange.BodyProfileMarkings);

        BodyProfileChanged?.Invoke(_store.State.BodyProfile);
    }

    private void RefreshDragVisuals()
    {
        foreach (var (id, card) in _selectedCards)
        {
            var isDragged = id == _draggedMarkingId;
            var isTarget = _draggedMarkingId != null && id == _dropTargetMarkingId && id != _draggedMarkingId;
            card.SetDragState(isDragged, isTarget);
        }
    }

    private void AddMarking(MarkingPrototype prototype)
    {
        if (_store.State.SelectedPart == null)
            return;

        var partPath = _store.State.SelectedPart.Path;
        var owningSet = FindOwningSet(prototype);
        var setMarkingIds = owningSet != null
            ? new HashSet<string>(owningSet.Markings.Select(m => m.Id))
            : null;

        _store.MutateBodyProfile(p =>
        {
            var pref = EnsurePreference(p.Root, partPath);

            var existingIndex = pref.Markings.FindIndex(m => m.MarkingId == prototype.ID);
            if (existingIndex >= 0)
            {
                if (owningSet != null && setMarkingIds != null)
                {
                    var current = pref.Markings.Count(m => setMarkingIds.Contains(m.MarkingId));
                    if (current <= owningSet.MinCount)
                        return;
                }
                pref.Markings.RemoveAt(existingIndex);
                return;
            }

            if (owningSet != null && setMarkingIds != null && owningSet.MaxCount > 0)
            {
                while (pref.Markings.Count(m => setMarkingIds.Contains(m.MarkingId)) >= owningSet.MaxCount)
                {
                    var oldest = pref.Markings.FindIndex(m => setMarkingIds.Contains(m.MarkingId));
                    if (oldest < 0)
                        break;
                    pref.Markings.RemoveAt(oldest);
                }
            }

            pref.Markings.Add(prototype.AsMarking());
        }, BodyEditorChange.BodyProfileMarkings);

        BodyProfileChanged?.Invoke(_store.State.BodyProfile);
    }

    private void RemoveMarking(string markingId)
    {
        if (_store.State.SelectedPart == null || _markingManager == null)
            return;

        MarkingSetPrototype? owningSet = null;
        HashSet<string>? setMarkingIds = null;
        if (_markingManager.Markings.TryGetValue(markingId, out var proto))
        {
            owningSet = FindOwningSet(proto);
            if (owningSet != null)
                setMarkingIds = [.. owningSet.Markings.Select(m => m.Id)];
        }

        var partPath = _store.State.SelectedPart.Path;
        _store.MutateBodyProfile(p =>
        {
            var pref = FindPreference(p.Root, partPath);
            if (pref == null)
                return;

            if (owningSet != null && setMarkingIds != null)
            {
                var current = pref.Markings.Count(m => setMarkingIds.Contains(m.MarkingId));
                if (current <= owningSet.MinCount)
                    return;
            }

            for (var i = pref.Markings.Count - 1; i >= 0; i--)
            {
                if (pref.Markings[i].MarkingId == markingId)
                    pref.Markings.RemoveAt(i);
            }
        }, BodyEditorChange.BodyProfileMarkings);

        BodyProfileChanged?.Invoke(_store.State.BodyProfile);
    }

    private static BodyPartPreference? FindPreference(BodyPartPreference root, BodyPartAddress path)
    {
        var node = root;
        foreach (var segment in path.Segments)
        {
            if (segment == "root")
                continue;
            if (!node.Children.TryGetValue(segment, out var child))
                return null;
            node = child;
        }
        return node;
    }

    private static BodyPartPreference EnsurePreference(BodyPartPreference root, BodyPartAddress path)
    {
        var node = root;
        foreach (var segment in path.Segments)
        {
            if (segment == "root")
                continue;
            if (!node.Children.TryGetValue(segment, out var child))
            {
                child = new BodyPartPreference();
                node.Children[segment] = child;
            }
            node = child;
        }
        return node;
    }
}
