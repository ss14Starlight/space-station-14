// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._Starlight.Lobby.UI;

public sealed class BodyEditorStore
{
    private static readonly RsiDirection[] _directions =
    [
        RsiDirection.South,
        RsiDirection.West,
        RsiDirection.North,
        RsiDirection.East,
    ];

    public BodyEditorState State { get; private set; } = new();

    public event Action<BodyEditorState, BodyEditorChange>? StateChanged;

    public void Dispatch(IBodyEditorAction action)
    {
        var (next, change) = Reduce(State, action);
        if (change == BodyEditorChange.None)
            return;

        State = next;
        StateChanged?.Invoke(State, change);
    }

    public void MutateBodyProfile(Action<BodyProfile> mutate, BodyEditorChange change)
    {
        mutate(State.BodyProfile);
        if (change == BodyEditorChange.None)
            return;

        StateChanged?.Invoke(State, change);
    }

    private static (BodyEditorState State, BodyEditorChange Change) Reduce(BodyEditorState state, IBodyEditorAction action)
    {
        switch (action)
        {
            case BodyEditorSetProfileAction setProfile:
            {
                var character = BodyEditorCharacterState.FromProfile(setProfile.Profile, state.Character.BodyRoot)
                    ?? state.Character with { HasProfile = false };
                return (state with { Character = character }, BodyEditorChange.Profile);
            }

            case BodyEditorSetBodyTreeAction setBodyTree:
            {
                var character = state.Character.WithBodyRoot(setBodyTree.Root);
                var keepSelection = ContainsPart(setBodyTree.Root, state.SelectedPartPath);
                var selectedPath = keepSelection ? state.SelectedPartPath : null;
                var change = BodyEditorChange.BodyRoot;
                if (!keepSelection && state.SelectedPartPath != null)
                    change |= BodyEditorChange.SelectedPart;
                return (state with { Character = character, SelectedPartPath = selectedPath }, change);
            }

            case BodyEditorSetBodyProfileAction setBodyProfile:
                return (state with { BodyProfile = setBodyProfile.Profile }, BodyEditorChange.BodyProfile);

            case BodyEditorSelectPartAction selectPart:
                if (state.SelectedPartPath == selectPart.Path)
                    return (state, BodyEditorChange.None);
                return (state with { SelectedPartPath = selectPart.Path }, BodyEditorChange.SelectedPart);

            case BodyEditorRotateAction rotate:
            {
                var next = Rotate(state.Direction, rotate.Offset);
                if (next == state.Direction)
                    return (state, BodyEditorChange.None);
                return (state with { Direction = next }, BodyEditorChange.Direction);
            }

            default:
                return (state, BodyEditorChange.None);
        }
    }

    private static RsiDirection Rotate(RsiDirection current, int offset)
    {
        var index = Array.IndexOf(_directions, current);
        if (index == -1)
            index = 0;

        return _directions[(index + offset + _directions.Length) % _directions.Length];
    }

    private static bool ContainsPart(BodyEditorBodyPartState? part, BodyPartAddress? path)
    {
        if (part == null || path == null)
            return false;

        var target = path.Value.PartOnly();
        if (part.Path == target)
            return true;

        foreach (var child in part.Children)
        {
            if (ContainsPart(child, path))
                return true;
        }

        return false;
    }
}
