// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
namespace Content.Shared._Starlight.Body.Events;

/// <summary>
/// Raised on a part when it is inserted into its parent's child container. Container-level: says nothing about
/// which body the part belongs to; use <see cref="BodyPartEnteredBodyEvent"/> for that.
/// </summary>
[ByRefEvent]
public record struct SLBodyPartAddedEvent(Entity<SLBodyPartComponent> Part);

/// <summary>
/// Raised on a part when it is removed from its parent's child container.
/// </summary>
[ByRefEvent]
public record struct SLBodyPartRemovedEvent(Entity<SLBodyPartComponent> Part);

/// <summary>
/// Raised on the part and then on the body when a part becomes part of a body: when the body is built, or when the
/// part (or a subtree containing it) is attached. Raised once for every part of an attached subtree, parents first.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartEnteredBodyEvent(Entity<SLBodyComponent> Body, Entity<SLBodyPartComponent> Part);

/// <summary>
/// Raised on the part and then on the body when a part stops being part of a body because it (or one of its
/// ancestors) was detached. Raised once for every part of the detached subtree, children first.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartLeftBodyEvent(Entity<SLBodyComponent> Body, Entity<SLBodyPartComponent> Part);
