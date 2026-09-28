using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Zones;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Zones;

public sealed partial class ZoneSystem
{
    [Dependency] private IAdminManager _admin = default!;

    private const int RoomViewChunkRadius = 4;

    [SubscribeNetworkEvent]
    private void OnRequestRooms(RequestZoneRoomsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEditTarget(ev.Grid, args.SenderSession, out var ent))
            return;

        var comp = ent.Value.Comp;
        var centre = ChunkOrigin(ev.Centre);

        var chunks = new List<ZoneRoomChunk>();
        var zones = new Dictionary<ushort, ProtoId<ZonePrototype>>();

        for (var x = -RoomViewChunkRadius; x <= RoomViewChunkRadius; x++)
        for (var y = -RoomViewChunkRadius; y <= RoomViewChunkRadius; y++)
        {
            var origin = centre + new Vector2i(x, y);

            if (!comp.Chunks.TryGetValue(origin, out var chunk))
                continue;

            var rooms = new ushort[ChunkArea];
            var any = false;

            for (var i = 0; i < rooms.Length; i++)
            {
                var room = chunk.Regions[i];

                if (room == NoRegion)
                    continue;

                room = FindRoot(comp, room);
                rooms[i] = room;
                any = true;

                if (zones.ContainsKey(room) ||
                    GetZone(comp.Regions[room].Zone) is not { } zone)
                    continue;

                zones[room] = zone;
            }

            if (any)
                chunks.Add(new ZoneRoomChunk(origin, rooms));
        }

        RaiseNetworkEvent(new ZoneRoomsSyncEvent(ev.Grid, chunks, zones), args.SenderSession);
    }

    [SubscribeNetworkEvent]
    private void OnRequestShapes(RequestZoneShapesEvent ev, EntitySessionEventArgs args)
    {
        if (TryGetEditTarget(ev.Grid, args.SenderSession, out var ent))
            SendZoneShapes(ent.Value, args.SenderSession);
    }

    [SubscribeNetworkEvent]
    private void OnRequestRect(RequestZoneRectEvent ev, EntitySessionEventArgs args)
    {
        if (TryGetEditTarget(ev.Grid, args.SenderSession, out var ent)
            && PaintZoneRect(ent.Value, ev.Rect, ev.Zone))
            SendZoneShapes(ent.Value, args.SenderSession);
    }

    /// <summary>
    /// Paints a rectangle of a zone onto the grid, merging with existing shapes and removing overlaps.
    /// </summary>
    public bool PaintZoneRect(Entity<ZoneGridComponent> ent, Box2i rect, ProtoId<ZonePrototype> zone)
    {
        if (IsEmpty(rect) || !Proto.HasIndex(zone))
            return false;

        var shapes = ent.Comp.Shapes;

        foreach (var other in shapes)
        {
            SubtractFrom(other.Rects, rect);
            MergeAdjacent(other.Rects);

            other.Circles.RemoveAll(circle => rect.ContainsTile(TileOf(circle.Center)));
        }

        shapes.RemoveAll(x => x.IsEmpty && x.Zone != zone);

        var set = shapes.Find(x => x.Zone == zone);

        if (set == null)
        {
            set = new ZoneShapeSet { Zone = zone };
            shapes.Add(set);
        }

        set.Rects.Add(rect);
        MergeAdjacent(set.Rects);

        ApplyEdit(ent);
        return true;
    }

    [SubscribeNetworkEvent]
    private void OnRequestErase(RequestZoneEraseEvent ev, EntitySessionEventArgs args)
    {
        if (TryGetEditTarget(ev.Grid, args.SenderSession, out var ent)
            && EraseZoneRect(ent.Value, ev.Rect))
            SendZoneShapes(ent.Value, args.SenderSession);
    }

    /// <summary>
    /// Erases a rectangle of zones from the grid, removing overlaps and merging adjacent shapes.
    /// </summary>
    public bool EraseZoneRect(Entity<ZoneGridComponent> ent, Box2i rect)
    {
        if (IsEmpty(rect))
            return false;

        var shapes = ent.Comp.Shapes;

        foreach (var set in shapes)
        {
            SubtractFrom(set.Rects, rect);
            MergeAdjacent(set.Rects);

            set.Circles.RemoveAll(circle => rect.ContainsTile(TileOf(circle.Center)));
        }

        shapes.RemoveAll(set => set.IsEmpty);

        ApplyEdit(ent);
        return true;
    }

    private bool TryGetEditTarget(
        NetEntity net,
        ICommonSession session,
        [NotNullWhen(true)] out Entity<ZoneGridComponent>? ent)
    {
        ent = null;

        if (!_admin.HasAdminFlag(session, AdminFlags.Spawn) ||
            !TryGetEntity(net, out var grid) ||
            !_mapGridQuery.HasComp(grid))
            return false;

        ent = (grid.Value, EnsureComp<ZoneGridComponent>(grid.Value));
        return true;
    }

    private void ApplyEdit(Entity<ZoneGridComponent> ent)
    {
        RebuildHints(ent);
        QueueFullRebuild(ent);
    }

    /// <summary>
    /// Sends the current zone shapes to a client session.
    /// </summary>
    public void SendZoneShapes(Entity<ZoneGridComponent> ent, ICommonSession session)
        => RaiseNetworkEvent(new ZoneShapesSyncEvent(GetNetEntity(ent.Owner), ent.Comp.Shapes), session);

    private static void SubtractFrom(List<Box2i> rects, Box2i cut)
    {
        for (var i = rects.Count - 1; i >= 0; i--)
        {
            var rect = rects[i];

            if (!Overlaps(rect, cut))
                continue;

            rects.RemoveAt(i);

            var left = Math.Max(rect.Left, cut.Left);
            var bottom = Math.Max(rect.Bottom, cut.Bottom);
            var right = Math.Min(rect.Right, cut.Right);
            var top = Math.Min(rect.Top, cut.Top);

            if (rect.Bottom < bottom)
                rects.Add(new Box2i(rect.Left, rect.Bottom, rect.Right, bottom));

            if (top < rect.Top)
                rects.Add(new Box2i(rect.Left, top, rect.Right, rect.Top));

            if (rect.Left < left)
                rects.Add(new Box2i(rect.Left, bottom, left, top));

            if (right < rect.Right)
                rects.Add(new Box2i(right, bottom, rect.Right, top));
        }
    }

    private static void MergeAdjacent(List<Box2i> rects)
    {
        var merged = true;

        while (merged)
        {
            merged = false;

            for (var i = 0; i < rects.Count && !merged; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                {
                    if (!TryMerge(rects[i], rects[j], out var union))
                        continue;

                    rects[i] = union;
                    rects.RemoveAt(j);
                    merged = true;
                    break;
                }
            }
        }
    }

    private static bool TryMerge(Box2i a, Box2i b, out Box2i union)
    {
        union = default;

        if (Contains(a, b))
        {
            union = a;
            return true;
        }

        if (Contains(b, a))
        {
            union = b;
            return true;
        }

        if (a.Bottom == b.Bottom && a.Top == b.Top && (a.Right == b.Left || b.Right == a.Left))
        {
            union = new Box2i(Math.Min(a.Left, b.Left), a.Bottom, Math.Max(a.Right, b.Right), a.Top);
            return true;
        }

        if (a.Left == b.Left && a.Right == b.Right && (a.Top == b.Bottom || b.Top == a.Bottom))
        {
            union = new Box2i(a.Left, Math.Min(a.Bottom, b.Bottom), a.Right, Math.Max(a.Top, b.Top));
            return true;
        }

        return false;
    }

    private static bool Contains(Box2i outer, Box2i inner)
        => outer.Left <= inner.Left && outer.Bottom <= inner.Bottom &&
            outer.Right >= inner.Right && outer.Top >= inner.Top;

    private static Vector2i TileOf(System.Numerics.Vector2 position)
        => new((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));

    private static bool IsEmpty(Box2i rect)
        => rect.Left >= rect.Right || rect.Bottom >= rect.Top;

    private static bool Overlaps(Box2i a, Box2i b)
        => a.Left < b.Right && b.Left < a.Right && a.Bottom < b.Top && b.Bottom < a.Top;
}
