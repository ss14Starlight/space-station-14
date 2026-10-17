// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Body.Systems;

/// <summary>
/// Body event relay: an event raised on a body is handed to every subscribed handler once per body part.
/// One relay exists per event type and owns the single (<see cref="SLBodyComponent"/>, TEvent) subscription,
/// so an event type must be subscribed either by value or by ref, never both.
/// Subscribe during your system's Initialize.
/// </summary>
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
        if (_relayHandlers.TryGetValue(evType, out var rawRelay))
        {
            var existing = (Relay<TEvent>)rawRelay;
            if (existing.IsByRef != isByRef)
                throw new InvalidOperationException(
                    $"Body event {evType.Name} is already relayed {(existing.IsByRef ? "by ref" : "by value")}; subscribe to it the same way.");
            return existing;
        }

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
    private sealed record Relay<TEvent> : Relay where TEvent : notnull
    {
        public readonly bool IsByRef;
        public event BodyEventHandler<TEvent>? Handlers = null;
        public event BodyRefEventHandler<TEvent>? RefHandlers = null;
        private ValueList<CompRelay> _compRelays = new();
        private readonly Dictionary<Type, CompRelay> _relayLookup = new();

        public Relay(SLBodySystem Self, bool isByRef) : base(Self)
        {
            IsByRef = isByRef;
            if (isByRef)
                Self.SubscribeLocalEvent<SLBodyComponent, TEvent>(HandleRefEvent);
            else
                Self.SubscribeLocalEvent<SLBodyComponent, TEvent>(HandleEvent);
        }

        private void HandleEvent(EntityUid uid, SLBodyComponent component, TEvent args)
        {
            var body = new Entity<SLBodyComponent>(uid, component);
            var parts = Self.GetBodyParts(body);
            if (Handlers != null)
            {
                for (var i = 0; i < parts.Count; i++)
                    Handlers.Invoke(body, parts[i], args);
            }

            foreach (var relay in _compRelays)
                relay.Raise(body, parts, Self, args);
        }

        private void HandleRefEvent(Entity<SLBodyComponent> body, ref TEvent args)
        {
            var parts = Self.GetBodyParts(body);
            if (RefHandlers != null)
            {
                for (var i = 0; i < parts.Count; i++)
                    RefHandlers.Invoke(body, parts[i], ref args);
            }

            foreach (var relay in _compRelays)
                relay.RaiseRef(body, parts, Self, ref args);
        }

        public CompRelay<TComp> EnsureCompRelay<TComp>() where TComp : IComponent
        {
            var compType = typeof(TComp);
            if (_relayLookup.TryGetValue(compType, out var rawRelay))
                return (CompRelay<TComp>)rawRelay;
            var relay = new CompRelay<TComp>();
            _compRelays.Add(relay);
            _relayLookup.Add(compType, relay);
            return relay;
        }

        public abstract class CompRelay
        {
            public abstract void Raise(Entity<SLBodyComponent> body, IReadOnlyList<Entity<SLBodyPartComponent>> parts,
                SLBodySystem self, TEvent args);
            public abstract void RaiseRef(Entity<SLBodyComponent> body, IReadOnlyList<Entity<SLBodyPartComponent>> parts,
                SLBodySystem self, ref TEvent args);
        }

        public sealed class CompRelay<TComp> : CompRelay where TComp : IComponent
        {
            public event BodyEventHandler<TComp,TEvent>? Handlers = null;
            public event BodyRefEventHandler<TComp,TEvent>? RefHandlers = null;
            public override void Raise(Entity<SLBodyComponent> body, IReadOnlyList<Entity<SLBodyPartComponent>> parts,
                SLBodySystem self, TEvent args)
            {
                if (Handlers == null || !self.TryComp<TComp>(body, out var comp))
                    return;
                for (var i = 0; i < parts.Count; i++)
                    Handlers.Invoke((body, body, comp), parts[i], args);
            }

            public override void RaiseRef(Entity<SLBodyComponent> body, IReadOnlyList<Entity<SLBodyPartComponent>> parts,
                SLBodySystem self, ref TEvent args)
            {
                if (RefHandlers == null || !self.TryComp<TComp>(body, out var comp))
                    return;
                for (var i = 0; i < parts.Count; i++)
                    RefHandlers.Invoke((body, body, comp), parts[i], ref args);
            }
        }
    }
}
