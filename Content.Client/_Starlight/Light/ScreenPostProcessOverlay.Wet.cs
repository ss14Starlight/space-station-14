using System.Numerics;
using Content.Shared.Fluids.Components;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

public sealed partial class ScreenPostProcessOverlay
{
    private static readonly ProtoId<ShaderPrototype> _wetMaskShader = "WetMask";

    /// <summary>
    /// Resolution of the wet floor mask relative to the viewport.
    /// </summary>
    private const float WetScale = 0.25f;

    private ShaderInstance? _wetTileShaderInst;
    private ShaderInstance? _wetSpriteShaderInst;

    private List<Entity<MapGridComponent>> _wetGrids = [];
    private readonly HashSet<Entity<PuddleComponent>> _puddles = [];
    private readonly List<(Matrix3x2 Transform, int Start, int Count)> _shinyGrids = [];
    private readonly List<(Box2 Box, float Shine)> _shinyTiles = [];
    private readonly List<(Entity<SpriteComponent> Sprite, Vector2 Position, Angle Rotation)> _wetSprites = [];

    /// <summary>
    /// Draws polished tiles and puddles into a soft mask, its alpha says how glossy the floor is.
    /// Returns null when nothing in view is glossy.
    /// </summary>
    private Texture? DrawWet(in OverlayDrawArgs args)
    {
        var mapId = args.MapId;
        if (mapId == MapId.Nullspace || !CollectWet(mapId, args.WorldBounds, args.WorldAABB))
            return null;

        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * WetScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * WetScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Wet?.Size != size)
        {
            res.Wet?.Dispose();
            res.Wet = _clyde.CreateRenderTarget(size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
                _smoothSampling,
                name: "wet-floor");
        }

        if (_wetTileShaderInst == null || _wetSpriteShaderInst == null)
        {
            _wetTileShaderInst = _proto.Index(_wetMaskShader).InstanceUnique();
            _wetTileShaderInst.SetParameter("softness", 0.02f);
            _wetTileShaderInst.SetParameter("fromTexture", 0f);
            _wetSpriteShaderInst = _proto.Index(_wetMaskShader).InstanceUnique();
            _wetSpriteShaderInst.SetParameter("softness", 0.02f);
            _wetSpriteShaderInst.SetParameter("fromTexture", 1f);
        }

        var matrix = res.Wet.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var handle = args.WorldHandle;
        var eyeRotation = viewport.Eye!.Rotation;

        handle.RenderInRenderTarget(res.Wet,
            () =>
            {
                handle.UseShader(_wetTileShaderInst);
                foreach (var (transform, start, count) in _shinyGrids)
                {
                    handle.SetTransform(Matrix3x2.Multiply(transform, matrix));
                    for (var i = start; i < start + count; i++)
                    {
                        var (box, shine) = _shinyTiles[i];
                        handle.DrawRect(box, new Color(1f, 1f, 1f, shine));
                    }
                }

                var spriteHandle = new TransformedWorldHandle(handle, matrix);
                foreach (var (sprite, position, rotation) in _wetSprites)
                {
                    spriteHandle.UseShader(_wetSpriteShaderInst);
                    _sprite.RenderSprite(sprite, spriteHandle, eyeRotation, rotation, position);
                }
            }, Color.Transparent);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        _shinyGrids.Clear();
        _shinyTiles.Clear();
        _wetSprites.Clear();
        return res.Wet.Texture;
    }

    private bool CollectWet(MapId mapId, Box2Rotated worldBounds, Box2 worldAABB)
    {
        _shinyGrids.Clear();
        _shinyTiles.Clear();
        _wetSprites.Clear();

        _wetGrids.Clear();
        _map.FindGridsIntersecting(mapId, worldBounds, ref _wetGrids);
        foreach (var grid in _wetGrids)
        {
            var start = _shinyTiles.Count;
            var tiles = _map.GetTilesIntersecting(grid.Owner, grid, worldBounds);
            while (tiles.MoveNext(out var tileRef))
            {
                if (_tileDefs[tileRef.Tile.TypeId] is not ContentTileDefinition { Shine: > 0f } def
                    || HasWall(grid, tileRef.GridIndices))
                    continue;

                var bounds = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                _shinyTiles.Add((bounds, Math.Clamp(def.Shine, 0f, 1f)));
            }

            if (_shinyTiles.Count > start)
                _shinyGrids.Add((_xform.GetWorldMatrix(grid.Owner), start, _shinyTiles.Count - start));
        }

        _puddles.Clear();
        _lookup.GetEntitiesIntersecting(mapId, worldAABB.Enlarged(1f), _puddles,
            LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Approximate);
        foreach (var puddle in _puddles)
        {
            if (!_entMan.TryGetComponent<SpriteComponent>(puddle.Owner, out var sprite) || !sprite.Visible)
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(puddle.Owner);
            _wetSprites.Add(((puddle.Owner, sprite), position, rotation));
        }

        _puddles.Clear();
        return _shinyTiles.Count > 0 || _wetSprites.Count > 0;
    }

    /// <summary>
    /// Whether a wall or another solid occluder stands on the tile, so its floor can't be seen.
    /// </summary>
    private bool HasWall(Entity<MapGridComponent> grid, Vector2i indices)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (_entMan.TryGetComponent<OccluderComponent>(uid, out var occluder) && occluder.Enabled)
                return true;
        }

        return false;
    }
}
