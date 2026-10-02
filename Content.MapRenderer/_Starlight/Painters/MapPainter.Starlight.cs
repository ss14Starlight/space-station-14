using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Parallax;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.MapRenderer.Painters;

public sealed partial class MapPainter
{
    private const string DefaultParallax = "Default";

    private bool _loadedFileIsGrid;

    private static bool IsGridFile(LoadResult result)
        => result.Category switch
        {
            FileCategory.Grid => true,
            FileCategory.Map => false,
            _ => result.Maps.Count == 0,
        };

    public MapViewerType GetMapType()
        => _map is RenderMapFile && _loadedFileIsGrid ? MapViewerType.Shuttle : MapViewerType.Station;

    public string GetViewerId()
    {
        if (_pair == null)
            throw new InvalidOperationException("Instance not initialized!");

        var fileName = _map is RenderMapPrototype prototype
            ? _pair.Server.ProtoMan.Index(prototype.Prototype).MapPath.FilenameWithoutExtension
            : Path.GetFileNameWithoutExtension(((RenderMapFile) _map).FileName);

        return fileName.ToLowerInvariant();
    }

    public async Task<string> GetDisplayName()
    {
        if (_pair == null)
            throw new InvalidOperationException("Instance not initialized!");

        if (_map is RenderMapPrototype prototype)
            return _pair.Server.ProtoMan.Index(prototype.Prototype).MapName;

        var name = string.Empty;
        if (GetMapType() == MapViewerType.Shuttle && _grids.Length == 1)
        {
            await _pair.Server.WaitPost(() => name = _pair.Server.EntMan.GetComponent<MetaDataComponent>(_grids[0].Owner).EntityName);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Equals("grid", StringComparison.OrdinalIgnoreCase))
            name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(_map.ShortName.Replace('_', ' ').Replace('-', ' '));

        return name;
    }

    public async Task<string> GetParallaxId()
    {
        if (_pair == null)
            throw new InvalidOperationException("Instance not initialized!");

        var parallax = DefaultParallax;
        await _pair.Server.WaitPost(() =>
        {
            var entMan = _pair.Server.EntMan;
            foreach (var (uid, _) in _grids)
            {
                var mapUid = entMan.GetComponent<TransformComponent>(uid).MapUid;
                if (entMan.TryGetComponent(mapUid, out ParallaxComponent? comp) && !string.IsNullOrEmpty(comp.Parallax))
                {
                    parallax = comp.Parallax;
                    return;
                }
            }
        });

        return parallax;
    }

    public async Task<ViewerParallaxGroup?> ExportParallax(ParallaxExporter exporter)
    {
        if (_pair == null)
            throw new InvalidOperationException("Instance not initialized!");

        var parallaxId = await GetParallaxId();
        var layers = exporter.Export(parallaxId, _pair.Client);
        if (layers.Count == 0)
            return null;

        return new ViewerParallaxGroup { Layers = layers.ToList() };
    }
}
