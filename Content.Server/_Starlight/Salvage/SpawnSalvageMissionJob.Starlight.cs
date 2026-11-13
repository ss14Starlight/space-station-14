// ReSharper disable CheckNamespace

using System.Threading.Tasks;
using Content.Server.Salvage.Expeditions;
using Content.Shared.Procedural;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Storage;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Salvage;

public sealed partial class SpawnSalvageMissionJob
{
    private async Task<bool> TrySpawnMegafauna(Entity<MapGridComponent> grid,
        SalvageExpeditionComponent expedition, SalvageFactionPrototype faction, Dungeon dungeon, IRobustRandom random)
    {
        if (faction.Megafauna is not { } megafauna
            || await SpawnRandomEntry(grid, new SalvageMobEntry { Proto = megafauna }, dungeon, random))
            return true;

        _sawmill.Warning($"Rejecting salvage expedition: no free tile for required boss {megafauna}.");
        expedition.GenerationFailed = true;
        _entManager.DeleteEntity(grid);
        return false;
    }

    private async Task SpawnFactionReinforcements(Entity<MapGridComponent> grid,
        SalvageFactionPrototype faction, Dungeon dungeon, IRobustRandom random)
    {
        foreach (var proto in EntitySpawnCollection.GetSpawns(faction.ExtraMobs, random))
            await SpawnRandomEntry(grid, new SalvageMobEntry { Proto = proto }, dungeon, random);
    }
}
