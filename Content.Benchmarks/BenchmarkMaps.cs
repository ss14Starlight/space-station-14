#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Benchmarks;

/// <summary>
/// Map paths and prototype IDs used by benchmarks on this fork.
/// Prefer Sol maps when available; skip Starlight maps that Sol replaces.
/// </summary>
internal static partial class BenchmarkMaps
{
    public const string ReachPath = "/Maps/_Sol/Stations/Reach.yml";
    public const string CorkPath = "/Maps/_Sol/Stations/Cork.yml";
    public const string SalternPath = "/Maps/_Starlight/Stations/Saltern.yml";

    public const string ReachPrototype = "SolReach";
    public const string CorkPrototype = "SolCork";
    public const string SalternPrototype = "StarlightSaltern";

    /// <summary>
    /// Every Sol and Starlight <c>gameMap</c> prototype, excluding Starlight maps
    /// that have a Sol replacement of the same short name (e.g. SolReach → skip StarlightReach).
    /// </summary>
    public static string[] AllLoadableGameMapIds { get; } = DiscoverAllLoadableGameMapIds();

    [GeneratedRegex(@"^\s*id:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex GameMapIdRegex();

    [GeneratedRegex(@"^\s*-?\s*type:\s*gameMap\s*$", RegexOptions.Multiline)]
    private static partial Regex GameMapTypeRegex();

    private static string[] DiscoverAllLoadableGameMapIds()
    {
        var resources = FindResourcesDirectory();
        if (resources is null)
        {
            return
            [
                ReachPrototype,
                CorkPrototype,
                SalternPrototype,
            ];
        }

        var solIds = new List<string>();
        var starlightIds = new List<string>();
        var solShortNames = new HashSet<string>(StringComparer.Ordinal);

        CollectGameMapIds(Path.Combine(resources, "Prototypes", "_Sol", "Maps"), solIds);
        CollectGameMapIds(Path.Combine(resources, "Prototypes", "_Starlight", "Maps"), starlightIds);

        foreach (var id in solIds)
        {
            if (id.StartsWith("Sol", StringComparison.Ordinal))
                solShortNames.Add(id["Sol".Length..]);
        }

        var filteredStarlight = starlightIds.Where(id =>
        {
            if (!id.StartsWith("Starlight", StringComparison.Ordinal))
                return true;

            var shortName = id["Starlight".Length..];
            return !solShortNames.Contains(shortName);
        });

        var result = solIds
            .Concat(filteredStarlight)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        if (result.Length == 0)
        {
            return
            [
                ReachPrototype,
                CorkPrototype,
                SalternPrototype,
            ];
        }

        return result;
    }

    private static void CollectGameMapIds(string mapsDir, List<string> destination)
    {
        if (!Directory.Exists(mapsDir))
            return;

        foreach (var file in Directory.EnumerateFiles(mapsDir, "*.yml", SearchOption.AllDirectories))
        {
            // Pool prototypes are not loadable station maps.
            if (file.Contains($"{Path.DirectorySeparatorChar}Pools{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            if (!GameMapTypeRegex().IsMatch(text))
                continue;

            foreach (Match match in GameMapIdRegex().Matches(text))
            {
                var id = match.Groups[1].Value;
                if (id.StartsWith("Sol", StringComparison.Ordinal) ||
                    id.StartsWith("Starlight", StringComparison.Ordinal))
                {
                    destination.Add(id);
                }
            }
        }
    }

    private static string? FindResourcesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Resources");
            if (Directory.Exists(Path.Combine(candidate, "Prototypes")))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
