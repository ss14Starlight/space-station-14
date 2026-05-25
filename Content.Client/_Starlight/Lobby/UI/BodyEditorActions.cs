// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Content.Shared.Preferences;

namespace Content.Client._Starlight.Lobby.UI;

public interface IBodyEditorAction;

public sealed record BodyEditorSetProfileAction(HumanoidCharacterProfile? Profile) : IBodyEditorAction;

public sealed record BodyEditorSetBodyTreeAction(BodyEditorBodyPartState? Root) : IBodyEditorAction;

public sealed record BodyEditorSetBodyProfileAction(BodyProfile Profile) : IBodyEditorAction;

public sealed record BodyEditorSelectPartAction(BodyPartAddress? Path) : IBodyEditorAction;

public sealed record BodyEditorRotateAction(int Offset) : IBodyEditorAction;
