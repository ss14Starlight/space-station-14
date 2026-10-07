using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.MapRenderer.Extensions;
using Content.MapRenderer.Painters;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer;

internal sealed partial class Program
{
    private const string MapsDirectory = "maps";
    private const string GridsDirectory = "map";
    private const string MapJson = "map.json";
    private const string IndexJson = "index.json";

    private static async Task<List<RenderMap>> CollectDirectoryMaps(CommandLineArguments arguments, ExternalTestContext testContext)
    {
        var maps = new List<RenderMap>();
        if (arguments.Directories.Count == 0)
            return maps;

        var files = new List<string>();
        foreach (var directory in arguments.Directories)
        {
            var path = directory;
            if (!Directory.Exists(path) && !Path.IsPathRooted(path))
                path = Path.Combine(DirectoryExtensions.RepositoryRoot().FullName, directory);

            if (!Directory.Exists(path))
            {
                await Console.Error.WriteLineAsync($"Directory {directory} doesn't exist!");
                continue;
            }

            var found = Directory.EnumerateFiles(path, "*.yml", SearchOption.AllDirectories)
                .Where(IsMapFile)
                .Select(Path.GetFullPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"Found {found.Count} map files in {path}");
            files.AddRange(found);
        }

        files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0)
            return maps;

        Dictionary<string, GameMapPrototype> prototypesByPath;
        await using (var pair = await PoolManager.GetServerClient(testContext: testContext))
        {
            prototypesByPath = pair.Server
                .ResolveDependency<IPrototypeManager>()
                .EnumeratePrototypes<GameMapPrototype>()
                .Where(map => !pair.IsTestPrototype(map))
                .GroupBy(map => map.MapPath.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(map => map.ID).First(), StringComparer.OrdinalIgnoreCase);
        }

        var resources = DirectoryExtensions.Resources().FullName;
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(resources, file).Replace('\\', '/');
            if (!relative.StartsWith("..") && prototypesByPath.TryGetValue($"/{relative}", out var prototype))
                maps.Add(new RenderMapPrototype { Prototype = prototype.ID });
            else
                maps.Add(new RenderMapFile { FileName = file });
        }

        Console.WriteLine($"Maps to render:\n{string.Join('\n', maps.Select(m => $"  {m}"))}");
        return maps;
    }

    private static bool IsMapFile(string path)
    {
        var firstLine = File.ReadLines(path).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return firstLine?.TrimStart('﻿').Trim() == "meta:";
    }

    private static async Task RunViewerExport(
        CommandLineArguments arguments,
        List<RenderMap> toRender,
        ExternalTestContext testContext)
    {
        var output = arguments.OutputPath;
        Directory.CreateDirectory(output);

        if (arguments.ExportViewerJson && arguments.BaseUrl == null)
            Console.WriteLine("Warning: no --base-url given, map.json will hold urls relative to the output directory. " + "The viewer only resolves them if the output is served from the site root.");

        var parallax = arguments.OutputParallax ? new ParallaxExporter(output, Url) : null;
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexEntries = new List<ViewerIndexEntry>();

        foreach (var map in toRender)
        {
            Console.WriteLine($"Painting map {map}");

            await using var painter = new MapPainter(map, testContext);

            ViewerMap viewerMap;
            string mapDirectory;
            try
            {
                await painter.Initialize();
                await painter.SetupView(showMarkers: arguments.ShowMarkers);

                var mapId = painter.GetViewerId();
                for (var n = 2; !usedIds.Add(mapId); n++)
                {
                    mapId = $"{painter.GetViewerId()}-{n}";
                }

                mapDirectory = Path.Combine(output, MapsDirectory, mapId);
                viewerMap = new ViewerMap { MapId = mapId };

                var gridsDirectory = Path.Combine(mapDirectory, GridsDirectory);
                var newGridsDirectory = gridsDirectory + ".new";
                if (Directory.Exists(newGridsDirectory))
                    Directory.Delete(newGridsDirectory, true);

                var i = 0;
                await foreach (var renderedGrid in painter.Paint())
                {
                    using var image = renderedGrid.Image;
                    var gridDirectory = Path.Combine(newGridsDirectory, i.ToString());

                    var tiles = TileSlicer.Slice(image, gridDirectory, arguments.TileSize, arguments.Format);
                    Console.WriteLine($"Wrote grid {i} of size {image.Width}x{image.Height} as {tiles} tiles to {gridDirectory}");

                    viewerMap.Grids.Add(ToViewerGrid(renderedGrid, i.ToString(), Url($"{MapsDirectory}/{mapId}/{GridsDirectory}/{i}"), arguments.TileSize));
                    i++;
                }

                if (Directory.Exists(gridsDirectory))
                    Directory.Delete(gridsDirectory, true);
                if (Directory.Exists(newGridsDirectory))
                    Directory.Move(newGridsDirectory, gridsDirectory);

                viewerMap.DisplayName = await painter.GetDisplayName();
                viewerMap.Type = arguments.ForcedType ?? painter.GetMapType();

                if (parallax != null && await painter.ExportParallax(parallax) is { } group)
                    viewerMap.ParallaxLayers.Add(group);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Painting map {map} failed due to an internal exception:");
                Console.WriteLine(ex);
                continue;
            }

            if (arguments.ExportViewerJson)
            {
                var mapJsonPath = Path.Combine(mapDirectory, MapJson);

                viewerMap.Attribution = ReadExistingAttribution(mapJsonPath);

                await File.WriteAllTextAsync(mapJsonPath, JsonSerializer.Serialize(viewerMap, ViewerMap.JsonOptions));
                Console.WriteLine($"Wrote {viewerMap.Type} {viewerMap.DisplayName} to {mapJsonPath}");

                indexEntries.Add(new ViewerIndexEntry
                {
                    MapId = viewerMap.MapId,
                    DisplayName = viewerMap.DisplayName,
                    Url = Url($"{MapsDirectory}/{viewerMap.MapId}/{MapJson}"),
                    Type = viewerMap.Type,
                });
            }

            try
            {
                await painter.CleanReturnAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while shutting down painter: {e}");
            }
        }

        if (arguments.ExportViewerJson && indexEntries.Count > 0)
            await UpdateIndex(Path.Combine(output, IndexJson), indexEntries);

        Console.WriteLine($"Processed {indexEntries.Count}/{toRender.Count} maps into {Path.GetFullPath(output)}");
        Console.WriteLine($"It's now safe to manually exit the process (automatic exit in a few moments...)");

        string Url(string relative)
            => arguments.BaseUrl == null ? relative : $"{arguments.BaseUrl}/{relative}";
    }

    private static ViewerGrid ToViewerGrid(RenderedGridImage<Rgba32> grid, string gridId, string url, int tileSize)
    {
        var width = grid.Image.Width;
        var height = grid.Image.Height;

        var bottomLeft = (grid.Offset + grid.LocalOrigin) * EyeManager.PixelsPerMeter;

        return new ViewerGrid
        {
            GridId = gridId,
            DisplayName = string.IsNullOrWhiteSpace(grid.Name) ? null : grid.Name,
            TileSize = tileSize,
            Offset = new ViewerPoint(bottomLeft.X, -bottomLeft.Y - height),
            Extent = new ViewerExtent(new ViewerPoint(0, 0), new ViewerPoint(width, height)),
            Url = url,
        };
    }

    private static string? ReadExistingAttribution(string mapJsonPath)
    {
        if (!File.Exists(mapJsonPath))
            return null;

        try
        {
            return JsonNode.Parse(File.ReadAllText(mapJsonPath))?["attribution"]?.GetValue<string>();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task UpdateIndex(string indexPath, List<ViewerIndexEntry> entries)
    {
        var index = new JsonArray();
        if (File.Exists(indexPath))
        {
            try
            {
                index = JsonNode.Parse(await File.ReadAllTextAsync(indexPath)) as JsonArray ?? [];
            }
            catch (JsonException e)
            {
                Console.WriteLine($"Existing {indexPath} is broken and will be rewritten: {e.Message}");
            }
        }

        foreach (var entry in entries)
        {
            var node = index
                .OfType<JsonObject>()
                .FirstOrDefault(n => string.Equals(n["mapId"]?.GetValue<string>(), entry.MapId, StringComparison.OrdinalIgnoreCase));

            if (node == null)
            {
                node = [];
                index.Add(node);
            }

            var updated = JsonSerializer.SerializeToNode(entry, ViewerMap.JsonOptions)!.AsObject();
            foreach (var (key, value) in updated)
            {
                node[key] = value?.DeepClone();
            }
        }

        await File.WriteAllTextAsync(indexPath, index.ToJsonString(ViewerMap.JsonOptions));
        Console.WriteLine($"Updated {indexPath} with {entries.Count} maps");
    }
}
