// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
namespace Content.Shared._Starlight.Body.Events;

[ByRefEvent]
public record struct SLBodyPartAddedEvent(Entity<SLBodyPartComponent> Part);

[ByRefEvent]
public record struct SLBodyPartRemovedEvent(Entity<SLBodyPartComponent> Part);
