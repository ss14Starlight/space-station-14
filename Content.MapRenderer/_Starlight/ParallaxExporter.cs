using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Content.Client.Parallax;
using Content.Client.Parallax.Data;
using Nett;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static Robust.UnitTesting.RobustIntegrationTest;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer;

public sealed class ParallaxExporter
{
    public const string DirectoryName = "parallax";

    private static readonly Size _generatedSize = new(1920, 1080);

    private readonly string _outputPath;
    private readonly Func<string, string> _url;

    private readonly Dictionary<string, List<ViewerParallaxLayer>> _parallaxes = [];

    private readonly HashSet<string> _written = [];

    public ParallaxExporter(string outputPath, Func<string, string> url)
    {
        _outputPath = outputPath;
        _url = url;

        var directory = Path.Combine(_outputPath, DirectoryName);
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    public IReadOnlyList<ViewerParallaxLayer> Export(string parallaxId, ClientIntegrationInstance client)
    {
        if (_parallaxes.TryGetValue(parallaxId, out var cached))
            return cached;

        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var resMan = client.ResolveDependency<IResourceManager>();
        var sawmill = client.ResolveDependency<ILogManager>().GetSawmill("parallax.export");

        var layers = new List<ViewerParallaxLayer>();
        _parallaxes[parallaxId] = layers;

        if (!protoMan.TryIndex<ParallaxPrototype>(parallaxId, out var prototype))
        {
            Console.WriteLine($"Warning: parallax prototype {parallaxId} doesn't exist, the map will have no parallax.");
            return layers;
        }

        var configs = prototype.Layers.Count > 0 ? prototype.Layers : prototype.LayersLQ;
        foreach (var config in configs)
        {
            if (!config.Tiled)
            {
                Console.WriteLine($"Parallax {parallaxId}: skipping non-tiled layer {Describe(config.Texture)}");
                continue;
            }

            if (config.Sprite)
            {
                Console.WriteLine($"Parallax {parallaxId}: skipping sprite layer {config.RSI}");
                continue;
            }

            string? path;
            try
            {
                path = WriteTexture(config, resMan, sawmill);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Parallax {parallaxId}: failed to export layer {Describe(config.Texture)}: {e}");
                continue;
            }

            if (path == null)
                continue;

            var movement = 1f - config.Slowness;
            layers.Add(new ViewerParallaxLayer
            {
                Url = _url(path),
                ParallaxScale = new ViewerPoint(movement, movement),
            });
        }

        Console.WriteLine($"Exported parallax {parallaxId} with {layers.Count} layers");
        return layers;
    }

    private string? WriteTexture(ParallaxLayerConfig config, IResourceManager resMan, ISawmill sawmill)
    {
        var scale = config.Scale;
        var scaleSuffix = scale == System.Numerics.Vector2.One
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"_{scale.X:0.###}x{scale.Y:0.###}");

        string key;
        Func<Image<Rgba32>> load;
        switch (config.Texture)
        {
            case ImageParallaxTextureSource image:
                key = image.Path.ToRelativePath().ToString();
                key = $"{Path.ChangeExtension(key, null)}{scaleSuffix}.png";
                load = () =>
                {
                    using var stream = resMan.ContentFileRead(image.Path);
                    return Image.Load<Rgba32>(stream);
                };
                break;

            case GeneratedParallaxTextureSource generated:
                var id = string.IsNullOrEmpty(generated.Identifier) ? "default" : generated.Identifier;
                key = $"generated/{id}{scaleSuffix}.png";
                load = () =>
                {
                    using var reader = new StreamReader(resMan.ContentFileRead(generated.ParallaxConfigPath));
                    var table = Toml.ReadString(reader.ReadToEnd().Replace(Environment.NewLine, "\n"));
                    return ParallaxGenerator.GenerateParallax(table, _generatedSize, sawmill, null);
                };
                break;

            default:
                Console.WriteLine($"Skipping parallax layer with unsupported texture source {config.Texture.GetType().Name}");
                return null;
        }

        var relative = $"{DirectoryName}/{key}";
        if (_written.Contains(key))
            return relative;

        using var texture = load();
        if (scaleSuffix.Length > 0)
        {
            var width = Math.Max(1, (int) MathF.Round(texture.Width * scale.X));
            var height = Math.Max(1, (int) MathF.Round(texture.Height * scale.Y));
            var sampler = scale.X >= 1 && scale.Y >= 1 ? KnownResamplers.NearestNeighbor : KnownResamplers.Bicubic;
            texture.Mutate(x => x.Resize(width, height, sampler));
        }

        var fullPath = Path.Combine(_outputPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        texture.SaveAsPng(fullPath);

        _written.Add(key);
        return relative;
    }

    private static string Describe(IParallaxTextureSource source)
        => source switch
        {
            ImageParallaxTextureSource image => image.Path.ToString(),
            GeneratedParallaxTextureSource generated => generated.ParallaxConfigPath.ToString(),
            _ => source.GetType().Name,
        };
}
