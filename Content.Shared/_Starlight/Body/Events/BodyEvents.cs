// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;

namespace Content.Shared._Starlight.Body.Events;
public delegate void BodyEventHandler<in TEvent>(
    Entity<SLBodyComponent> body,
    Entity<SLBodyPartComponent> bodyPart,
    TEvent args);

public delegate void BodyRefEventHandler<TEvent>(
    Entity<SLBodyComponent> body,
    Entity<SLBodyPartComponent> bodyPart,
    ref TEvent args);


public delegate void BodyEventHandler<TComp,in TEvent>(
    Entity<SLBodyComponent, TComp> body,
    Entity<SLBodyPartComponent> bodyPart,
    TEvent args)
    where TComp:IComponent;

public delegate void BodyRefEventHandler<TComp, TEvent>(
    Entity<SLBodyComponent, TComp> body,
    Entity<SLBodyPartComponent> bodyPart,
    ref TEvent args)
    where TComp:IComponent;
public struct BodyInitEvent;
