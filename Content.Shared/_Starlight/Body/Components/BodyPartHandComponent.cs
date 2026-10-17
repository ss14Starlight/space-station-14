// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Hands.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Body.Components;

/// <summary>
/// A body part that gives its body a hand while attached. The hand is named after the part's socket path and its
/// location follows the side the part is attached on, unless <see cref="Location"/> overrides it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BodyPartHandComponent : Component
{
    [DataField]
    public HandLocation? Location;

    /// <summary>
    /// Name of the hand this part added to the body, while attached.
    /// </summary>
    [ViewVariables]
    public string? HandId;
}
