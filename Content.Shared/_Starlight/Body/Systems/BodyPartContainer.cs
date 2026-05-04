// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Collections;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Body.Systems;

[UsedImplicitly]
[SerializedType(nameof(BodyPartContainer))]
public sealed partial class BodyPartContainer : BaseContainer,
    IEnumerable<Entity<SLBodyPartComponent>>,
    IEnumerable<(BodyPartSocket, Entity<SLBodyPartComponent>?)>
{
    private ValueList<EntityUid> _bodyParts = new();
    private ValueList<SLBodyPartComponent> _bodyPartComps = new();
    private readonly Dictionary<BodyPartSocket, int> _socketLookup = new();

    public override IReadOnlyList<EntityUid> ContainedEntities => _bodyParts.ToArray();
    public override int Count => _bodyParts.Count;
    public override bool Contains(EntityUid contained) => _bodyParts.Contains(contained);

    protected override void InternalInsert(EntityUid toInsert, IEntityManager entMan)
    {
        Entity<SLBodyPartComponent> toAdd = (toInsert, entMan.GetComponent<SLBodyPartComponent>(toInsert));
        _bodyParts.Add(toInsert);
        _bodyPartComps.Add(toAdd);
        if (toAdd.Comp.ParentSocket != null)
        {
            //Assert if the required socket either doesn't exist or already is occupied
            DebugTools.Assert(_socketLookup.TryGetValue(toAdd.Comp.ParentSocket.Value, out var idx) && idx == -1);
            _socketLookup[toAdd.Comp.ParentSocket.Value] = _bodyParts.Count - 1;
        }
        var ev = new SLBodyPartAddedEvent(toAdd);
        entMan.EventBus.RaiseLocalEvent(toAdd, ref ev);
    }

    protected override void InternalRemove(EntityUid toRemove, IEntityManager entMan)
    {
        var index = _bodyParts.IndexOf(toRemove);
        if (index == -1)
            return;
        Entity<SLBodyPartComponent> removed;
        if (_bodyParts.Count == 1) //if we only have a single part left, we don't need to worry about maintaining indices!
        {
            removed = (_bodyParts[index], _bodyPartComps[index]);
            _bodyParts.RemoveAt(index);
            _bodyPartComps.RemoveAt(index);
            var removedEv = new SLBodyPartRemovedEvent(removed);
            entMan.EventBus.RaiseLocalEvent(removed, ref removedEv);
            _socketLookup.Clear();
            return;
        }
        //We use remove swap so that we can maintain socket indices
        removed = (_bodyParts.RemoveSwap(index), _bodyPartComps.RemoveSwap(index));
        var ev = new SLBodyPartRemovedEvent(removed);
        entMan.EventBus.RaiseLocalEvent(removed, ref ev);
        if (removed.Comp.ParentSocket != null) //If our removed socket is in the lookup, remove the lookup
            _socketLookup[removed.Comp.ParentSocket.Value] = -1;
        if (index >= _bodyPartComps.Count)
            return;
        var movedComp = _bodyPartComps[index]; //Update the socket lookup for the component that got moved!
        if (movedComp.ParentSocket != null) _socketLookup[movedComp.ParentSocket.Value] = index;
    }

    protected override void InternalShutdown(IEntityManager entMan, SharedContainerSystem system, bool isClient)
    {
        foreach (var entity in _bodyParts)
        {
            if (!isClient)
                entMan.DeleteEntity(entity);
            else if (entMan.EntityExists(entity))
                system.Remove(entity, this, reparent: false, force: true);
        }
    }

    public bool TryGetBodyPart(BodyPartSocket socket,
        out Entity<SLBodyPartComponent> bodyPart)
    {
        if (!_socketLookup.TryGetValue(socket, out var idx) || idx == -1)
        {
            bodyPart = default;
            return false;
        }

        bodyPart = (_bodyParts[idx], _bodyPartComps[idx]);
        return true;
    }

    public bool TryRegisterSocket(BodyPartSocket socket) => _socketLookup.TryAdd(socket, -1);

    IEnumerator<(BodyPartSocket, Entity<SLBodyPartComponent>?)> IEnumerable<(BodyPartSocket, Entity<SLBodyPartComponent>?)>.GetEnumerator() => new SocketEnumerable(this);

    public IEnumerator<Entity<SLBodyPartComponent>> GetEnumerator() => new Enumerable(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerable(BodyPartContainer container) : IEnumerator<Entity<SLBodyPartComponent>>
    {
        private int _index = 0;
        private readonly BodyPartContainer _container = container;


        public bool MoveNext()
        {
            _index++;
            if (_index < _container.Count) return true;
            _index = _container.Count - 1;
            return false;
        }

        public void Reset() => _index = 0;

        Entity<SLBodyPartComponent> IEnumerator<Entity<SLBodyPartComponent>>.Current => new Entity<SLBodyPartComponent>(_container._bodyParts[_index], _container._bodyPartComps[_index]);

        object? IEnumerator.Current => new Entity<SLBodyPartComponent>(_container._bodyParts[_index], _container._bodyPartComps[_index]);

        public void Dispose(){}
    }

    public struct SocketEnumerable(BodyPartContainer container) : IEnumerator<(BodyPartSocket, Entity<SLBodyPartComponent>?)>
    {
        private Dictionary<BodyPartSocket, int>.Enumerator _enumerator = container._socketLookup.GetEnumerator();
        public bool MoveNext() => _enumerator.MoveNext();

        public void Reset()
        {
            _enumerator = container._socketLookup.GetEnumerator();
        }

        (BodyPartSocket, Entity<SLBodyPartComponent>?) IEnumerator<(BodyPartSocket, Entity<SLBodyPartComponent>?)>.
            Current
        {
            get
            {
                var idx = _enumerator.Current.Value;
                if (idx == -1)
                {
                    return (_enumerator.Current.Key, null);
                }
                return (_enumerator.Current.Key,
                    new Entity<SLBodyPartComponent>(container._bodyParts[idx], container._bodyPartComps[idx]));
            }
        }

        object? IEnumerator.Current
        {
            get
            {
                var idx = _enumerator.Current.Value;
                if (idx == -1)
                    return null;
                return (_enumerator.Current.Key,
                    new Entity<SLBodyPartComponent>(container._bodyParts[idx], container._bodyPartComps[idx]));
            }
        }

        public void Dispose() => _enumerator.Dispose();
    }
}
