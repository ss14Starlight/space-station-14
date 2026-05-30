// SPDX-FileCopyrightText: 2026 Starlight // Starlight
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Content.Server.Administration.Systems;

public sealed partial class StarlightEntitySystem
{
    #region TryEntity

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T>(EntityUid uid, [NotNullWhen(true)] out Entity<T> entity, bool log = true)
        where T : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error("Entity {EntityUid} invalid", uid);
            return false;
        }

        if (!TryComp<T>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T));
            return false;
        }

        entity = (uid, comp1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error($"Entity {uid} invalid");
            return false;
        }

        if (!TryComp<T1>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));
            return false;
        }

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));
            return false;
        }

        entity = (uid, comp1, comp2);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2, T3>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2, T3> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error($"Entity {uid} invalid");
            return false;
        }

        if (!TryComp<T1>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));
            return false;
        }

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));
            return false;
        }

        if (!TryComp<T3>(uid, out var comp3))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));
            return false;
        }

        entity = (uid, comp1, comp2, comp3);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2, T3, T4>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2, T3, T4> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error($"Entity {uid} invalid");
            return false;
        }

        if (!TryComp<T1>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));
            return false;
        }

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));
            return false;
        }

        if (!TryComp<T3>(uid, out var comp3))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));
            return false;
        }

        if (!TryComp<T4>(uid, out var comp4))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));
            return false;
        }

        entity = (uid, comp1, comp2, comp3, comp4);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2, T3, T4, T5>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2, T3, T4, T5> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
        where T5 : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error($"Entity {uid} invalid");
            return false;
        }

        if (!TryComp<T1>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));
            return false;
        }

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));
            return false;
        }

        if (!TryComp<T3>(uid, out var comp3))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));
            return false;
        }

        if (!TryComp<T4>(uid, out var comp4))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));
            return false;
        }

        if (!TryComp<T5>(uid, out var comp5))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T5));
            return false;
        }

        entity = (uid, comp1, comp2, comp3, comp4, comp5);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2, T3, T4, T5, T6>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2, T3, T4, T5, T6> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
        where T4 : class, IComponent
        where T5 : class, IComponent
        where T6 : class, IComponent
    {
        entity = default;
        if (!uid.IsValid() || !TryComp(uid, out MetaDataComponent? metadata))
        {
            _sawmill.Error($"Entity {uid} invalid");
            return false;
        }

        if (!TryComp<T1>(uid, out var comp1))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T1));
            return false;
        }

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T2));
            return false;
        }

        if (!TryComp<T3>(uid, out var comp3))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T3));
            return false;
        }

        if (!TryComp<T4>(uid, out var comp4))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T4));
            return false;
        }

        if (!TryComp<T5>(uid, out var comp5))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T5));
            return false;
        }

        if (!TryComp<T6>(uid, out var comp6))
        {
            if (log) _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata.EntityName, metadata.EntityPrototype, uid, typeof(T6));
            return false;
        }

        entity = (uid, comp1, comp2, comp3, comp4, comp5, comp6);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEntity<T1, T2>(Entity<T1> source, [NotNullWhen(true)] out Entity<T1, T2> entity, bool log = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
    {
        entity = default;
        var uid = source.Owner;

        if (!TryComp<T2>(uid, out var comp2))
        {
            if (log)
            {
                TryComp(uid, out MetaDataComponent? metadata);
                _sawmill.Error("Entity {EntityName}[{EntityPrototype}]:{EntityUid} does not have component {type}", metadata?.EntityName, metadata?.EntityPrototype, uid, typeof(T2));
            }
            return false;
        }

        entity = (uid, source.Comp, comp2);
        return true;
    }

    #endregion
}
