// ReSharper disable CheckNamespace

using Content.Shared.Cargo.Components;

namespace Content.Server.Station.Systems;

public sealed partial class StationSystem
{
    /// <summary>
    /// Resolves ITG's shared station through the ATS, falling back to the entity's owning station
    /// while the ATS has no unique station owner.
    /// </summary>
    public EntityUid? GetItgStation(Entity<TransformComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        var entityUid = entity.Owner;
        var fallbackStation = GetOwningStation(entityUid, entity.Comp);
        EntityUid? tradeStationOwner = null;
        var query = EntityQueryEnumerator<TradeStationComponent>();

        while (query.MoveNext(out var tradeStationUid, out _))
        {
            var station = GetOwningStation(tradeStationUid);
            if (station is null)
                continue;

            if (tradeStationOwner is { } existing && existing != station.Value)
            {
                _sawmill.Warning(
                    $"Multiple stations own TradeStation grids; falling back to the owning station of entity {entityUid}.");
                return fallbackStation;
            }

            tradeStationOwner = station.Value;
        }

        return tradeStationOwner ?? fallbackStation;
    }
}
