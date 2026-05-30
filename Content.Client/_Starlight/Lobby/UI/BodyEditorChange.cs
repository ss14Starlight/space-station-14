// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// Identifies which slices of <see cref="BodyEditorState"/> were touched by a dispatched action.
/// </summary>
[Flags]
public enum BodyEditorChange
{
    None = 0,
    Profile = 1 << 0,
    /// <summary>
    /// The whole <see cref="BodyEditorState.BodyProfile"/> reference was replaced
    /// </summary>
    BodyProfile = 1 << 1,
    BodyRoot = 1 << 2,
    SelectedPart = 1 << 3,
    Direction = 1 << 4,
    /// <summary>
    /// One or more <see cref="BodyProfile.Parameters"/> entries were mutated in place
    /// </summary>
    BodyProfileColors = 1 << 5,
    /// <summary>
    /// The marking tree under <see cref="BodyProfile.Root"/> was mutated in place
    /// </summary>
    BodyProfileMarkings = 1 << 6,
}
