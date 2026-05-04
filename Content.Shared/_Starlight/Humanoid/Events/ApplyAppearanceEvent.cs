// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Preferences;

namespace Content.Shared._Starlight.Humanoid.Events;

/// <summary>
///     Raised directed on a freshly spawned entity.
///     Use this to apply markings and appearance to body parts.
/// </summary>
[ByRefEvent]
public record struct ApplyAppearanceEvent(HumanoidCharacterProfile? Profile); // todo The profile will be reworked into a more acceptable version later.
