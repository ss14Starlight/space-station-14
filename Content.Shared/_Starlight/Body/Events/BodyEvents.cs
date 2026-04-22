// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;

namespace Content.Shared._Starlight.Body.Events;

public interface IBodyEvent<TEvent>
{
    Entity<SLBodyComponent> Body { get; set; }
    TEvent Args { get; set; }
}

public interface IBodyEvent<TComp, TEvent> : IBodyEvent<TEvent>
    where TComp:IComponent
{
    TComp SimComp { get; set; }
}


public struct BodyInitEvent;

public record struct BodyPartInitEvent(Entity<SLBodyComponent> Body);

public record struct BodyPartInitEvent<TComp>(Entity<SLBodyComponent> Body, TComp SimComp)
    where TComp : IComponent;
