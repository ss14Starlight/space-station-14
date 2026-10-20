using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer;

public sealed partial class CommandLineArguments
{
    public const int DefaultTileSize = 256;

    /// <summary>
    /// Slice every grid into <see cref="TileSize"/> tiles laid out the way the map viewer loads them.
    /// </summary>
    public bool Tiles { get; set; }

    public int TileSize { get; set; } = DefaultTileSize;

    /// <summary>
    /// Prefix for every url written into map.json. The viewer doesn't resolve grid urls relative to map.json,
    /// so without it the output only works when served from the site root.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Directories that get scanned for map files, every map found there is rendered.
    /// </summary>
    public List<string> Directories { get; set; } = new();

    /// <summary>
    /// Overrides the station/shuttle detection for every rendered map.
    /// </summary>
    public MapViewerType? ForcedType { get; set; }

    private bool TryParseStarlight(string argument, IEnumerator<string> enumerator, out bool valid)
    {
        valid = true;

        switch (argument)
        {
            case "-t":
            case "--tiles":
                Tiles = true;
                return true;

            case "--tile-size":
                if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out var tileSize) || tileSize <= 0)
                {
                    Console.WriteLine($"Invalid tile size specified for option: {argument}");
                    valid = false;
                    return true;
                }

                TileSize = tileSize;
                return true;

            case "--base-url":
                if (!enumerator.MoveNext() || string.IsNullOrWhiteSpace(enumerator.Current))
                {
                    Console.WriteLine($"No url specified for option: {argument}");
                    valid = false;
                    return true;
                }

                BaseUrl = enumerator.Current.Trim().TrimEnd('/');
                return true;

            case "-d":
            case "--dir":
                if (!enumerator.MoveNext() || string.IsNullOrWhiteSpace(enumerator.Current))
                {
                    Console.WriteLine($"No directory specified for option: {argument}");
                    valid = false;
                    return true;
                }

                Directories.Add(enumerator.Current);
                return true;

            case "--type":
                if (!enumerator.MoveNext() || !Enum.TryParse<MapViewerType>(enumerator.Current, true, out var type))
                {
                    Console.WriteLine($"Invalid map type specified for option: {argument}, expected station or shuttle");
                    valid = false;
                    return true;
                }

                ForcedType = type;
                return true;
        }

        return false;
    }

    private void ApplyStarlightDefaults()
    {
        if (!ExportViewerJson)
            return;

        Tiles = true;
        OutputParallax = true;
    }

    private static void PrintStarlightHelp()
        => Console.WriteLine(@"Starlight options:
    --viewer
        Writes everything the Starlight map viewer needs: sliced grids, map.json, index.json and parallax.
        Implies --tiles and --parallax. Layout of the output directory:
            index.json
            maps/<map>/map.json
            maps/<map>/map/<grid>/<x>/<y>/0
            parallax/...
    -t / --tiles
        Slices grids into tiles instead of writing one image per grid.
    --tile-size <pixels>
        Tile size used by --tiles. Defaults to 256.
    --base-url <url>
        Url the output directory will be served from, urls in map.json are prefixed with it.
        Example: --base-url https://raw.githubusercontent.com/ss14Starlight/Starlight.Maps/main
    -d / --dir <directory>
        Renders every map file in the directory and its subdirectories. Can be passed several times.
        Files used by a game map prototype are rendered as that prototype.
    --type <station|shuttle>
        Forces the map type written into map.json and index.json.
        By default files saved as a grid are shuttles, everything else is a station.

Example:
    Content.MapRenderer --viewer -d Resources/Maps/_Starlight/Stations -d Resources/Maps/_Starlight/Shuttles -o ../Starlight.Maps --base-url https://raw.githubusercontent.com/ss14Starlight/Starlight.Maps/main");
}
