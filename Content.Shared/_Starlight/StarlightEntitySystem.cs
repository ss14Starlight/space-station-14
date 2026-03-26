using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Systems;

public sealed partial class StarlightEntitySystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Robust.Shared.IoC.Dependency] private readonly ILogManager _logManager = default!;
    [Robust.Shared.IoC.Dependency] private readonly IPrototypeManager _prototypes = default!;

    ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("StarlightEntitySystem");

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

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

    #endregion

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

    #region TryGetNearestEntity

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNearestEntity<T>(EntityUid uid, [NotNullWhen(true)] out Entity<T> entity, bool sameGrid = true)
        where T : class, IComponent
    {
        var mainTransform = Transform(uid);
        var worldPosition = _transformSystem.GetWorldPosition(mainTransform);
        var entityQuery = EntityManager.EntityQueryEnumerator<T, TransformComponent>();
        entity = default;
        float? latestDistance = null;
        while (entityQuery.MoveNext(out var ent, out var comp, out var transform))
        {
            if (transform.GridUid == mainTransform.GridUid)
            {
                var currentDistance = Vector2.DistanceSquared(transform.LocalPosition, mainTransform.LocalPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp);
                }
            }
            else if (!sameGrid)
            {
                var currentDistance = Vector2.DistanceSquared(_transformSystem.GetWorldPosition(transform), worldPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp);
                }
            }
        }

        return entity != default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNearestEntity<T1, T2>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2> entity, bool sameGrid = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
    {
        var mainTransform = Transform(uid);
        var worldPosition = _transformSystem.GetWorldPosition(mainTransform);
        var entityQuery = EntityManager.EntityQueryEnumerator<T1, T2, TransformComponent>();
        entity = default;
        float? latestDistance = null;
        while (entityQuery.MoveNext(out var ent, out var comp1, out var comp2, out var transform))
        {
            if (transform.GridUid == mainTransform.GridUid)
            {
                var currentDistance = Vector2.DistanceSquared(transform.LocalPosition, mainTransform.LocalPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp1, comp2);
                }
            }
            else if (!sameGrid)
            {
                var currentDistance = Vector2.DistanceSquared(_transformSystem.GetWorldPosition(transform), worldPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp1, comp2);
                }
            }
        }

        return entity != default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNearestEntity<T1, T2, T3>(EntityUid uid, [NotNullWhen(true)] out Entity<T1, T2, T3> entity, bool sameGrid = true)
        where T1 : class, IComponent
        where T2 : class, IComponent
        where T3 : class, IComponent
    {
        var mainTransform = Transform(uid);
        var worldPosition = _transformSystem.GetWorldPosition(mainTransform);
        var entityQuery = EntityManager.EntityQueryEnumerator<T1, T2, T3, TransformComponent>();
        entity = default;
        float? latestDistance = null;
        while (entityQuery.MoveNext(out var ent, out var comp1, out var comp2, out var comp3, out var transform))
        {
            if (transform.GridUid == mainTransform.GridUid)
            {
                var currentDistance = Vector2.DistanceSquared(transform.LocalPosition, mainTransform.LocalPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp1, comp2, comp3);
                }
            }
            else if (!sameGrid)
            {
                var currentDistance = Vector2.DistanceSquared(_transformSystem.GetWorldPosition(transform), worldPosition);
                if (latestDistance < currentDistance)
                {
                    latestDistance = currentDistance;
                    entity = (ent, comp1, comp2, comp3);
                }
            }
        }

        return entity != default;
    }

    #endregion
}
