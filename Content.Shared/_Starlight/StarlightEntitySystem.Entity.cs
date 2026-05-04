// SPDX-FileCopyrightText: 2026 Starlight // Starlight
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace Content.Server.Administration.Systems;

public sealed partial class StarlightEntitySystem
{
    #region Entity

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T?> Entity<T>(EntityUid uid, bool log = true)
        where T : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T));

        return (uid, comp1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T1?, T2?> Entity<T1, T2>(EntityUid uid, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T1>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));

        if (!TryComp<T2>(uid, out var comp2) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));

        return (uid, comp1, comp2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T1?, T2?, T3?> Entity<T1, T2, T3>(EntityUid uid, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T1>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));

        if (!TryComp<T2>(uid, out var comp2) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));

        if (!TryComp<T3>(uid, out var comp3) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));

        return (uid, comp1, comp2, comp3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T1?, T2?, T3?, T4?> Entity<T1, T2, T3, T4>(EntityUid uid, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T1>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));

        if (!TryComp<T2>(uid, out var comp2) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));

        if (!TryComp<T3>(uid, out var comp3) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));

        if (!TryComp<T4>(uid, out var comp4) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));

        return (uid, comp1, comp2, comp3, comp4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T1?, T2?, T3?, T4?, T5?> Entity<T1, T2, T3, T4, T5>(EntityUid uid, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
        where T5 : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T1>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));

        if (!TryComp<T2>(uid, out var comp2) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));

        if (!TryComp<T3>(uid, out var comp3) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));

        if (!TryComp<T4>(uid, out var comp4) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));

        if (!TryComp<T5>(uid, out var comp5) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T5));

        return (uid, comp1, comp2, comp3, comp4, comp5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<T1?, T2?, T3?, T4?, T5?, T6?> Entity<T1, T2, T3, T4, T5, T6>(EntityUid uid, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
        where T5 : class, IComponent
        where T6 : class, IComponent
    {
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return uid;
        }

        if (!TryComp<T1>(uid, out var comp1) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));

        if (!TryComp<T2>(uid, out var comp2) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));

        if (!TryComp<T3>(uid, out var comp3) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));

        if (!TryComp<T4>(uid, out var comp4) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));

        if (!TryComp<T5>(uid, out var comp5) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T5));

        if (!TryComp<T6>(uid, out var comp6) && log)
            _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T6));

        return (uid, comp1, comp2, comp3, comp4, comp5, comp6);
    }

    #endregion
}
