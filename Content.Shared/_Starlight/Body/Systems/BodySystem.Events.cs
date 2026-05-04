// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Body.Systems;

public sealed partial class SLBodySystem
{
    private readonly Dictionary<Type, Relay> _relayHandlers = new();

    public void SubscribeBodyEvent<TEvent>(BodyEventHandler<TEvent> handler) where TEvent : notnull
    {
        var relay = EnsureRelay<TEvent>(false);
        relay.Handlers += handler;
    }

    public void SubscribeBodyEvent<TEvent>(BodyRefEventHandler<TEvent> handler) where TEvent : notnull
    {
        var relay = EnsureRelay<TEvent>(true);
        relay.RefHandlers += handler;
    }

    public void SubscribeBodyEvent<TParentComp,TEvent>(BodyEventHandler<TParentComp,TEvent> handler) where TEvent : notnull where TParentComp : IComponent
    {
        var relay = EnsureCompRelay<TEvent, TParentComp>(false);
        relay.Handlers += handler;
    }

    public void SubscribeBodyEvent<TParentComp,TEvent>(BodyRefEventHandler<TParentComp,TEvent> handler) where TEvent : notnull where TParentComp : IComponent
    {
        var relay = EnsureCompRelay<TEvent, TParentComp>(true);
        relay.RefHandlers += handler;
    }

    private Relay<TEvent> EnsureRelay<TEvent>(bool isByRef) where TEvent : notnull
    {
        var evType = typeof(TEvent);
        if (_relayHandlers.TryGetValue(evType, out var rawRelay)) return (Relay<TEvent>)rawRelay;
        var relay = new Relay<TEvent>(this, isByRef);
        _relayHandlers.Add(evType, relay);
        return relay;
    }

    private Relay<TEvent>.CompRelay<TComp> EnsureCompRelay<TEvent, TComp>(bool isByRef) where TComp : IComponent where TEvent : notnull
    {
        var relay = EnsureRelay<TEvent>(isByRef);
        return relay.EnsureCompRelay<TComp>();
    }

    private abstract record Relay(SLBodySystem Self);
    private record Relay<TEvent> : Relay where TEvent : notnull
    {
        public event BodyEventHandler<TEvent>? Handlers = null;
        public event BodyRefEventHandler<TEvent>? RefHandlers = null;
        private ValueList<CompRelay> _compRelays = new();
        private Dictionary<Type, CompRelay> _relayLookup = new();

        public Relay(SLBodySystem Self, bool isByRef) : base(Self)
        {
            if (isByRef)
                Self.SubscribeLocalEvent<SLBodyComponent, TEvent>(HandleRefEvent);
            else
                Self.SubscribeLocalEvent<SLBodyComponent, TEvent>(HandleEvent);
        }

        private void HandleEvent(EntityUid uid, SLBodyComponent component, TEvent args)
        {
            var body = new Entity<SLBodyComponent>(uid, component);
            if (Handlers != null)
                foreach (var part in body.Comp.BodyParts)
                    Handlers.Invoke(body, part, args);
            foreach (var relay in _compRelays)
                relay.Raise(body, Self, args);
        }

        private void HandleRefEvent(Entity<SLBodyComponent> body, ref TEvent args)
        {
            if (RefHandlers != null)
                foreach (var part in body.Comp.BodyParts)
                    RefHandlers.Invoke(body, part,ref args);

            foreach (var relay in _compRelays)
                relay.RaiseRef(body, Self, ref args);
        }

        public CompRelay<TComp> EnsureCompRelay<TComp>() where TComp : IComponent
        {
            var evType = typeof(TEvent);
            if (_relayLookup.TryGetValue(evType, out var rawRelay)) return (CompRelay<TComp>)rawRelay;
            var relay = new CompRelay<TComp>();
            _compRelays.Add(relay);
            _relayLookup.Add(evType, relay);
            return relay;
        }

        public abstract class CompRelay
        {
            public abstract void Raise(Entity<SLBodyComponent> body,
                SLBodySystem self, TEvent args);
            public abstract void RaiseRef(Entity<SLBodyComponent> body,
                SLBodySystem self, ref TEvent args);
        }

        public sealed class CompRelay<TComp> : CompRelay where TComp : IComponent
        {
            public event BodyEventHandler<TComp,TEvent>? Handlers = null;
            public event BodyRefEventHandler<TComp,TEvent>? RefHandlers = null;
            public override void Raise(Entity<SLBodyComponent> body,
                SLBodySystem self, TEvent args)
            {
                if (Handlers == null || !self.TryComp<TComp>(body, out var comp))
                    return;
                foreach (var bodyPart in body.Comp.BodyParts)
                {
                    Handlers.Invoke((body, body, comp), bodyPart, args);
                }
            }

            public override void RaiseRef(Entity<SLBodyComponent> body,
                SLBodySystem self, ref TEvent args)
            {
                if (RefHandlers == null || !self.TryComp<TComp>(body, out var comp))
                    return;
                foreach (var bodyPart in body.Comp.BodyParts)
                {
                    RefHandlers.Invoke((body, body, comp), bodyPart, ref args);
                }
            }
        }
    }
}
