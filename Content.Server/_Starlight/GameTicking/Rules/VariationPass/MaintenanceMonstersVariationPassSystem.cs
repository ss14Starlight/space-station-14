using Content.Server.GameTicking.Rules.VariationPass.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.EntityTable;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Server.GameTicking.Rules.VariationPass;
using Content.Server._Starlight.GameTicking.Rules.VariationPass.Components;
using Content.Server.GameTicking.Rules;

namespace Content.Server._Starlight.GameTicking.Rules.VariationPass;

/// <summary>
/// Handles putting things in lockers around the station, intended for creatures.
/// </summary>
public sealed partial class MaintenanceMonstersVariationPassSystem : VariationPassSystem<MaintenanceMonstersVariationPassComponent>
{
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void ApplyVariation(Entity<MaintenanceMonstersVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        var query = AllEntityQuery<RoundstartMonsterSpawnComponent, EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var storage, out var transform))
        {
            // Ignore if not part of the station
            if (!IsMemberOfStation((uid, transform), ref args))
                continue;

            // If we don't hit the random chance for this one, skip
            if (!_random.Prob(ent.Comp.PerLockerProbability))
                continue;

            var protos = _entityTable.GetSpawns(ent.Comp.SpawnTable);

            foreach (var proto in protos)
            {
                var spawn = EntityManager.Spawn(proto, MapCoordinates.Nullspace);
                if (!_entityStorage.Insert(spawn, uid, storage))
                {
                    Del(spawn);
                    // We failed to put one in the storage, so we're likely to fail at putting the rest in; go to the next storage.
                    break;
                }
            }
        }
    }
}
