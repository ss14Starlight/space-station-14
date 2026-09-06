
using System.Collections.Generic;
using System.IO;
using Robust.Shared.Prototypes;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Utility;

public static partial class GameDataScrounger
{
    private static readonly YamlScalarNode _valuesNode = new("values");

    private static List<string>? _partialDiskPaths;
    private static List<string>? _partialVfsPaths;

    private static IEnumerable<string> GetPrototypeIds(YamlNode id)
    {
        if (id is YamlScalarNode { Value: { } scalarId })
        {
            yield return scalarId;
            yield break;
        }

        if (!(id is YamlMappingNode variants && variants.Tag == $"type:{nameof(CreateVariants)}" &&
            variants[_valuesNode] is YamlSequenceNode values))
            yield break;

        foreach (var value in values.Children)
        {
            if (value is YamlScalarNode { Value: { } variantId })
                yield return variantId;
        }
    }

    /// <summary>
    ///     Whether a VFS prototype path (like <c>/Prototypes/_Starlight/Partials/...</c>) is marked partial
    ///     by <c>Resources/PartialPrototypes</c>.
    /// </summary>
    public static bool IsPartialVfsPath(string path)
    {
        _partialVfsPaths ??= LoadMarkedPaths(ContentResources());

        foreach (var partial in _partialVfsPaths)
        {
            if (path == partial || path.StartsWith(partial + "/"))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Path equivalent of <see cref="IsPartialVfsPath"/>, for the prototype walk.
    /// </summary>
    private static bool IsPartialPath(string resDir, string path)
    {
        _partialDiskPaths ??= LoadPartialDiskPaths(resDir);

        if (_partialDiskPaths.Count == 0)
            return false;

        var normalized = Normalize(Path.GetFullPath(path));

        foreach (var partial in _partialDiskPaths)
        {
            if (normalized == partial || normalized.StartsWith(partial + "/"))
                return true;
        }

        return false;
    }

    private static List<string> LoadPartialDiskPaths(string resDir)
    {
        var paths = new List<string>();

        foreach (var marked in LoadMarkedPaths(resDir))
        {
            paths.Add(Normalize(Path.GetFullPath($"{resDir}/{marked.TrimStart('/')}")));
        }

        return paths;
    }

    /// <summary>
    ///     Reads the rooted paths listed in <c>Resources/PartialPrototypes</c>.
    /// </summary>
    private static List<string> LoadMarkedPaths(string resDir)
    {
        var paths = new List<string>();
        var markerDir = $"{resDir}/PartialPrototypes";

        if (!Directory.Exists(markerDir))
            return paths;

        foreach (var file in Directory.EnumerateFiles(markerDir, "*.yml"))
        {
            var stream = new YamlStream();
            using var reader = File.OpenText(file);
            stream.Load(reader);

            foreach (var document in stream)
            {
                if (document.RootNode is not YamlSequenceNode sequence)
                    continue;

                foreach (var child in sequence.Children)
                {
                    if (child is YamlScalarNode { Value: { } marked })
                        paths.Add("/" + marked.Trim().TrimStart('/'));
                }
            }
        }

        return paths;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
