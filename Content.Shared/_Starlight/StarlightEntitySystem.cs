// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

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
