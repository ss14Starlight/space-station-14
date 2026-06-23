using System.Numerics;
using Content.Shared.Pinpointer;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Silicons.StationAi;

/// <summary>
/// Handles building and drawing cyberspace navmap geometry (floor/wall/door tiles).
/// Formation (geometry rebuild) and drawing are both encapsulated here.
/// </summary>
internal sealed class CyberspaceNavMapRenderer(IPrototypeManager proto)
{
    private static readonly ProtoId<ShaderPrototype> _floorShader = "CyberspaceFloor";
    private static readonly ProtoId<ShaderPrototype> _wallShader = "CyberspaceWall";
    private static readonly ProtoId<ShaderPrototype> _doorShader = "CyberspaceDoor";

    private readonly List<Vector2> _floorVerts = [];
    private readonly List<Vector2> _wallVerts = [];
    private readonly List<Vector2> _doorVerts = [];

    private float _navDataTimer;
    private EntityUid _lastGridUid;
    private Vector2 _lastRebuildCenter;
    private const float NavDataUpdateInterval = 1.0f;
    private const float RebuildMoveThreshold = 8f;

    /// <summary>
    /// Updates geometry caches: rebuilds vertex arrays when grid, camera, or timer threshold changes.
    /// </summary>
    public void Update(float frameTime, EntityUid gridUid, NavMapComponent? navMap, MapGridComponent grid, SharedTransformSystem xforms, Box2Rotated worldBounds)
    {
        _navDataTimer += frameTime;
        var gridChanged = _lastGridUid != gridUid;
        if (gridChanged)
            _lastGridUid = gridUid;

        // Compute grid-local viewport bounds for chunk culling
        var gridInvMatrix = xforms.GetInvWorldMatrix(gridUid);
        var localAabb = gridInvMatrix.TransformBox(worldBounds);
        var viewCenter = localAabb.Center;
        var cameraMoved = (viewCenter - _lastRebuildCenter).LengthSquared() > RebuildMoveThreshold * RebuildMoveThreshold;

        if (navMap == null)
        {
            _floorVerts.Clear();
            _wallVerts.Clear();
            _doorVerts.Clear();
            return;
        }

        if (_navDataTimer >= NavDataUpdateInterval || gridChanged || cameraMoved)
        {
            _navDataTimer = 0f;
            _lastRebuildCenter = viewCenter;
            var cullBounds = localAabb.Enlarged(SharedNavMapSystem.ChunkSize * grid.TileSize);
            RebuildNavMapGeometry(navMap, grid.TileSize, cullBounds);
        }
    }

    /// <summary>
    /// Draws the cached navmap geometry using per-category cyberspace shaders.
    /// </summary>
    public void Draw(DrawingHandleWorld handle, Matrix3x2 transform)
    {
        handle.SetTransform(transform);
        DrawNavMap(handle);
        handle.UseShader(null);
    }

    private void DrawNavMap(DrawingHandleWorld handle)
    {
        Draw(_floorShader, _floorVerts);
        Draw(_wallShader, _wallVerts);
        Draw(_doorShader, _doorVerts);

        void Draw(ProtoId<ShaderPrototype> shader, List<Vector2> verts)
        {
            if (verts.Count == 0) return;
            handle.UseShader(proto.Index(shader).Instance());
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts, Color.White);
        }
    }

    /// <summary>
    /// Rebuilds floor/wall/door vertex arrays from NavMapComponent chunk bitmasks.
    /// Only processes chunks that intersect <paramref name="cullBounds"/> (grid-local space).
    /// Each tile that matches a category becomes a 1×1 quad (6 vertices, 2 triangles).
    /// </summary>
    private void RebuildNavMapGeometry(NavMapComponent navMap, int tileSize, Box2 cullBounds)
    {
        _floorVerts.Clear();
        _wallVerts.Clear();
        _doorVerts.Clear();

        var chunkWorldSize = SharedNavMapSystem.ChunkSize * tileSize;

        foreach (var chunk in navMap.Chunks.Values)
        {
            // Chunk AABB in grid-local space
            var chunkMinX = chunk.Origin.X * chunkWorldSize;
            var chunkMinY = chunk.Origin.Y * chunkWorldSize;
            var chunkBox = new Box2(chunkMinX, chunkMinY, chunkMinX + chunkWorldSize, chunkMinY + chunkWorldSize);

            if (!cullBounds.Intersects(chunkBox))
                continue;

            for (var i = 0; i < SharedNavMapSystem.ArraySize; i++)
            {
                var tileData = chunk.TileData[i];
                if (tileData == 0)
                    continue;

                var relative = SharedNavMapSystem.GetTileFromIndex(i);
                var gridTile = (chunk.Origin * SharedNavMapSystem.ChunkSize) + relative;

                var x = (float)(gridTile.X * tileSize);
                var y = (float)(gridTile.Y * tileSize);
                var s = (float)tileSize;

                if ((tileData & SharedNavMapSystem.FloorMask) != 0)
                    AddQuad(_floorVerts, x, y, s);

                if ((tileData & SharedNavMapSystem.WallMask) != 0)
                    AddQuad(_wallVerts, x, y, s);

                if ((tileData & SharedNavMapSystem.AirlockMask) != 0)
                    AddQuad(_doorVerts, x, y, s);
            }
        }
    }

    /// <summary>
    /// Appends 6 vertices (2 triangles) for a quad in grid-local space.
    /// </summary>
    private static void AddQuad(List<Vector2> verts, float x, float y, float size)
    {
        verts.Add(new Vector2(x, y));
        verts.Add(new Vector2(x + size, y));
        verts.Add(new Vector2(x + size, y + size));

        verts.Add(new Vector2(x, y));
        verts.Add(new Vector2(x + size, y + size));
        verts.Add(new Vector2(x, y + size));
    }
}
