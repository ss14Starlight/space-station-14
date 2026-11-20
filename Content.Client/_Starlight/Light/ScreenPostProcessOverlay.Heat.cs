using System.Numerics;
using Content.Client.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

public sealed partial class ScreenPostProcessOverlay
{
    private static readonly ProtoId<ShaderPrototype> _heatBlobShader = "HeatBlob";

    /// <summary>
    /// Resolution of the heat mask relative to the viewport.
    /// </summary>
    private const float HeatScale = 0.25f;

    /// <summary>
    /// Radius in tiles of the hot air around a burning tile or entity.
    /// </summary>
    private const float FireHeatRadius = 1.4f;

    private const float BurningHeatRadius = 1.1f;

    /// <summary>
    /// Heat of atmos fire by its fire state 1 to 3, and of a burning entity.
    /// </summary>
    private static readonly float[] _fireStateHeat = [0.5f, 0.75f, 1f];

    private const float BurningHeat = 0.8f;

    /// <summary>
    /// Most vertices sent in one heat draw. The engine copies each draw onto the stack, so it stays small.
    /// </summary>
    private const int MaxHeatVertices = 4096;

    private ShaderInstance? _heatBlobShaderInst;
    private List<Entity<MapGridComponent>> _heatGrids = [];

    // One list of spots per heat level, since a draw takes a single colour.
    private readonly List<Vector2>[] _heatSpots = [[], [], [], []];
    private readonly List<DrawVertexUV2D> _heatVertices = [];
    private readonly List<ushort> _heatIndices = new();

    /// <summary>
    /// Draws the heat around fires and burning things into a soft mask. Returns null when nothing is hot in view.
    /// </summary>
    private Texture? DrawHeat(in OverlayDrawArgs args)
    {
        var mapId = args.MapId;
        if (mapId == MapId.Nullspace)
            return null;

        var area = args.WorldAABB.Enlarged(FireHeatRadius);
        if (!CollectHeatSpots(mapId, args.WorldBounds, area))
            return null;

        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * HeatScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * HeatScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Heat?.Size != size)
        {
            res.Heat?.Dispose();
            res.Heat = _clyde.CreateRenderTarget(size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8),
                _smoothSampling,
                name: "heat-haze");
        }

        _heatBlobShaderInst ??= _proto.Index(_heatBlobShader).Instance();
        var matrix = res.Heat.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var handle = args.WorldHandle;

        handle.RenderInRenderTarget(res.Heat,
            () =>
            {
                handle.UseShader(_heatBlobShaderInst);
                handle.SetTransform(matrix);
                for (var level = 0; level < _heatSpots.Length; level++)
                {
                    var heat = level < _fireStateHeat.Length ? _fireStateHeat[level] : BurningHeat;
                    var radius = level < _fireStateHeat.Length ? FireHeatRadius : BurningHeatRadius;
                    DrawHeatSpots(handle, _heatSpots[level], radius, new Color(heat, heat, heat));
                }
            }, Color.Black);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        return res.Heat.Texture;
    }

    /// <summary>
    /// Gathers the world positions of burning tiles and entities in view. Returns false when there are none.
    /// </summary>
    private bool CollectHeatSpots(MapId mapId, Box2Rotated worldBounds, Box2 area)
    {
        var found = false;
        foreach (var spots in _heatSpots)
        {
            spots.Clear();
        }

        _heatGrids.Clear();
        _map.FindGridsIntersecting(mapId, area, ref _heatGrids);
        foreach (var grid in _heatGrids)
        {
            if (!_entMan.TryGetComponent<GasTileOverlayComponent>(grid.Owner, out var overlay))
                continue;

            var (_, _, worldMatrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
            var local = invMatrix.TransformBox(worldBounds).Enlarged(FireHeatRadius + grid.Comp.TileSize);
            var tileSize = grid.Comp.TileSize;

            foreach (var chunk in overlay.Chunks.Values)
            {
                var enumerator = new GasChunkEnumerator(chunk);
                while (enumerator.MoveNext(out var gas))
                {
                    if (gas.FireState == 0)
                        continue;

                    var index = chunk.Origin + new Vector2i(enumerator.X, enumerator.Y);
                    var centre = (index + new Vector2(0.5f)) * tileSize;
                    if (!local.Contains(centre))
                        continue;

                    var level = Math.Clamp(gas.FireState - 1, 0, _fireStateHeat.Length - 1);
                    _heatSpots[level].Add(Vector2.Transform(centre, worldMatrix));
                    found = true;
                }
            }
        }

        var burning = _entMan.EntityQueryEnumerator<FireVisualsComponent, TransformComponent>();
        while (burning.MoveNext(out _, out var fire, out var xform))
        {
            if (fire.LightEntity == null || xform.MapID != mapId)
                continue;

            var position = _xform.GetWorldPosition(xform);
            if (!area.Contains(position))
                continue;

            _heatSpots[_fireStateHeat.Length].Add(position);
            found = true;
        }

        return found;
    }

    private void DrawHeatSpots(DrawingHandleWorld handle, List<Vector2> spots, float radius, Color heat)
    {
        foreach (var spot in spots)
        {
            if (_heatVertices.Count + 4 > MaxHeatVertices)
                FlushHeat(handle, heat);

            var start = (ushort) _heatVertices.Count;
            _heatVertices.Add(new DrawVertexUV2D(spot + new Vector2(-radius, -radius), new Vector2(0f, 0f)));
            _heatVertices.Add(new DrawVertexUV2D(spot + new Vector2(radius, -radius), new Vector2(1f, 0f)));
            _heatVertices.Add(new DrawVertexUV2D(spot + new Vector2(radius, radius), new Vector2(1f, 1f)));
            _heatVertices.Add(new DrawVertexUV2D(spot + new Vector2(-radius, radius), new Vector2(0f, 1f)));
            _heatIndices.Add(start);
            _heatIndices.Add((ushort) (start + 1));
            _heatIndices.Add((ushort) (start + 2));
            _heatIndices.Add(start);
            _heatIndices.Add((ushort) (start + 2));
            _heatIndices.Add((ushort) (start + 3));
        }

        FlushHeat(handle, heat);
    }

    private void FlushHeat(DrawingHandleWorld handle, Color heat)
    {
        if (_heatVertices.Count == 0)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
            Texture.White,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_heatIndices),
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_heatVertices),
            heat);

        _heatVertices.Clear();
        _heatIndices.Clear();
    }
}
