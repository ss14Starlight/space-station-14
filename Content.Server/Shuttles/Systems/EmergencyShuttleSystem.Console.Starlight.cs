using System.Numerics;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private EntityUid? _evacuationPlanetMap = null;
    private EntityCoordinates? _evacuationLandingZone = null;
    private const float PodSpreadRadius = 25f;

    private void SetupEvacuationPlanet()
    {
        try
        {
            // Create a new map for the evacuation planet
            _mapSystem.CreateMap(out var mapId, runMapInit: false);
            _evacuationPlanetMap = _mapSystem.GetMap(mapId);

            if (_evacuationPlanetMap == null)
            {
                Log.Error("Failed to create evacuation planet map!");
                return;
            }

            // Generate a biome planet (similar to expedition/arrivals planets)
            var biomeOptions = new[]
            {
                "Grasslands",
                "Snow",
                "Caves"
            };

            var selectedBiome = _random.Pick(biomeOptions);

            if (!_protoManager.TryIndex<BiomeTemplatePrototype>(selectedBiome, out var template))
            {
                Log.Error($"Failed to load biome template: {selectedBiome}");
                return;
            }

            // Generate the planet biome
            _biomes.EnsurePlanet(_evacuationPlanetMap.Value, template);

            // Add ore layers to the biome for mining
            if (TryComp(_evacuationPlanetMap.Value, out BiomeComponent? biomeComp))
            {
                var oreMarkers = new[]
                {
                    "OreIron",
                    "OreCoal",
                    "OreQuartz",
                    "OreSalt",
                    "OreGold",
                    "OreSilver",
                    "OrePlasma",
                    "OreUranium",
                    "OreDiamond",
                    "OreArtifactFragment"
                };

                foreach (var oreId in oreMarkers)
                {
                    _biomes.AddMarkerLayer(_evacuationPlanetMap.Value, biomeComp, oreId);
                }
            }

            // Get the map's grid component for dungeon generation
            if (!TryComp<MapGridComponent>(_evacuationPlanetMap.Value, out var grid))
                return;

            var dungeonConfigs = new[]
            {
                "Experiment",
                "ShipWreckDungeon",
                "SovietDungeonWeh",
                "Mineshaft"
            };

            var numRuins = _random.Next(3, 6); // 3-5 ruins
            var selectedConfigs = _random.GetItems(dungeonConfigs, numRuins, allowDuplicates: false);
            var seed = _random.Next();
            var offsetDistance = 50f;

            foreach (var configId in selectedConfigs)
            {
                if (!_protoManager.TryIndex<DungeonConfigPrototype>(configId, out var dungeonProto))
                {
                    Log.Warning($"Could not load dungeon config {configId}");
                    continue;
                }

                // Calculate offset position for this ruin
                var angle = _random.NextAngle();
                var offset = angle.ToVec() * offsetDistance;
                var offsetPos = (Vector2i)(Vector2.Zero + offset);

                // Generate the dungeon
                try
                {
                    _dungeon.GenerateDungeon(dungeonProto, _evacuationPlanetMap.Value, grid, offsetPos, seed++);

                    Log.Debug($"Generated ruin {configId} at offset {offsetPos}");
                }
                catch (Exception e)
                {
                    Log.Warning($"Error generating ruin {configId}: {e.Message}");
                }
            }

            // Set landing zone at center of planet
            _evacuationLandingZone = new EntityCoordinates(_evacuationPlanetMap.Value, Vector2.Zero);

            // Initialize the map
            _mapSystem.InitializeMap(mapId);

            // Set a nice name
            _metaData.SetEntityName(_evacuationPlanetMap.Value, "Evacuation Planet");

            Log.Info($"Created evacuation planet with {selectedBiome} biome and {numRuins} ruins");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to setup evacuation planet: {ex}");
            _evacuationPlanetMap = null;
            _evacuationLandingZone = null;
        }
    }
}
