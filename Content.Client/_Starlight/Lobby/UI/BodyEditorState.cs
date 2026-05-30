// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed record BodyEditorState
{
    public ProtoId<BodyPrefabPrototype> BodyPrefab { get; init; } = "SLHumanBodyPrefab";
    public BodyEditorCharacterState Character { get; init; } = new();
    public BodyProfile BodyProfile { get; init; } = new();
    public BodyPartAddress? SelectedPartPath { get; init; }
    public RsiDirection Direction { get; init; } = RsiDirection.South;

    public BodyEditorBodyPartState? SelectedPart => FindPart(Character.BodyRoot, SelectedPartPath);
    public IReadOnlyList<BodyEditorBodyPartState> Parts => Flatten(Character.BodyRoot);

    private static BodyEditorBodyPartState? FindPart(BodyEditorBodyPartState? part, BodyPartAddress? path)
    {
        if (part == null || path == null)
            return null;

        // Marking-set-scoped addresses still resolve to the underlying body part state.
        var target = path.Value.PartOnly();
        if (part.Path == target)
            return part;

        foreach (var child in part.Children)
        {
            var found = FindPart(child, path);
            if (found != null)
                return found;
        }

        return null;
    }

    private static List<BodyEditorBodyPartState> Flatten(BodyEditorBodyPartState? root)
    {
        if (root == null)
            return [];

        var parts = new List<BodyEditorBodyPartState>();
        AddPart(root, parts);
        return parts;
    }

    private static void AddPart(BodyEditorBodyPartState part, List<BodyEditorBodyPartState> parts)
    {
        parts.Add(part);

        foreach (var child in part.Children)
            AddPart(child, parts);
    }
}
