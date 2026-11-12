using System.Numerics;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Tube;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.TransitTube;

/// <summary>
/// Decorations of transit tubes: the corner pieces that fill in diagonal joints.
/// Also hides the direction arrow on real pods: it is only meant for the placement ghost, which is client-side.
/// </summary>
public sealed partial class TransitTubeVisualsSystem : EntitySystem
{
    [Dependency] private DisposalTubeSystem _tube = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ResPath _rsi = new("/Textures/_Starlight/Structures/Piping/TransitTube/tube.rsi");
    private const int MaxCorners = 4;
    private const string PodArrowKey = "direction";

    /// <summary>
    /// Sprite layer holding a piece's glass, tinted by its material. Details stay untinted.
    /// </summary>
    private const string GlassKey = "glass";

    /// <summary>
    /// Tubes whose decorations need redoing. Updated a tick later, so a tube that is being deleted
    /// is already gone from its neighbours' point of view.
    /// </summary>
    private readonly HashSet<EntityUid> _dirty = [];

    [SubscribeLocalEvent]
    private void OnStartup(Entity<TransitTubeComponent> ent, ref ComponentStartup args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<TransitTubeComponent> ent, ref ComponentShutdown args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnMove(Entity<TransitTubeComponent> ent, ref MoveEvent args)
    {
        // A moved tube leaves neighbours behind and may join new ones.
        if (args.OldPosition.EntityId.IsValid() && args.OldPosition != args.NewPosition)
            MarkNeighbours(args.OldPosition);

        MarkWithNeighbours(ent);
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<TransitTubeComponent> ent, ref AnchorStateChangedEvent args)
        => MarkWithNeighbours(ent);

    [SubscribeLocalEvent]
    private void OnGlassStartup(Entity<TransitTubeGlassComponent> ent, ref ComponentStartup args)
        => _dirty.Add(ent);

    [SubscribeLocalEvent]
    private void OnGlassState(Entity<TransitTubeGlassComponent> ent, ref AfterAutoHandleStateEvent args)
        => _dirty.Add(ent);

    [SubscribeLocalEvent]
    private void OnPodStartup(Entity<TransitPodComponent> ent, ref ComponentStartup args)
    {
        if (IsClientSide(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), PodArrowKey, out var index, false))
            _sprite.LayerSetVisible((ent, sprite), index, false);
    }

    [SubscribeLocalEvent]
    private void OnPodAppearance(Entity<TransitPodComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        _appearance.TryGetData<TransitTubeLevel>(ent, TransitPodVisuals.Level, out var level, args.Component);
        _appearance.TryGetData<bool>(ent, TransitPodVisuals.Hidden, out var hidden, args.Component);

        var depth = level switch
        {
            TransitTubeLevel.Under => Shared.DrawDepth.DrawDepth.ThinWire,
            TransitTubeLevel.Over => Shared.DrawDepth.DrawDepth.Overdoors,
            _ => Shared.DrawDepth.DrawDepth.Mobs,
        };

        _sprite.SetDrawDepth((ent, sprite), (int)depth);
        _sprite.SetVisible((ent, sprite), !hidden);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirty.Count == 0)
            return;

        foreach (var uid in _dirty)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (TryComp<TransitTubeComponent>(uid, out var comp))
                UpdateDecorations((uid, comp));

            UpdateGlass(uid);
        }

        _dirty.Clear();
    }

    private void MarkWithNeighbours(EntityUid uid)
    {
        _dirty.Add(uid);
        MarkNeighbours(Transform(uid).Coordinates);
    }

    private void MarkNeighbours(EntityCoordinates coordinates)
    {
        if (_xform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, coordinates);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var anchored = _map.GetAnchoredEntities(gridUid, grid, center + new Vector2i(x, y));
                while (anchored.MoveNext(out var neighbour))
                {
                    if (HasComp<TransitTubeComponent>(neighbour))
                        _dirty.Add(neighbour.Value);
                }
            }
        }
    }

    private void UpdateDecorations(Entity<TransitTubeComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp) || !TryComp<DisposalTubeComponent>(ent, out var tube))
            return;

        var sprite = new Entity<SpriteComponent?>(ent, spriteComp);
        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var exits = _tube.GetTubeConnectableDirections((ent, tube));

        // Layers rotate with the tube, so corners are placed in the tube's own frame.
        var corner = 0;
        foreach (var exit in exits)
        {
            if (exit is not Direction.NorthEast and not Direction.NorthWest)
                continue;

            if (!xform.Anchored || _tube.GetTubeInDirection((ent, tube), exit) == null)
                continue;

            var local = new Angle(exit.ToAngle() - rotation).GetDir().ToIntVec();
            if (local.X == 0 || local.Y == 0)
                continue;

            SetCorner(sprite, corner++, new Vector2(local.X, 0), CornerState(-local.X, local.Y));
            SetCorner(sprite, corner++, new Vector2(0, local.Y), CornerState(local.X, -local.Y));
        }

        for (; corner < MaxCorners; corner++)
        {
            foreach (var key in new[] { CornerKey(corner), CornerDetailsKey(corner) })
            {
                if (_sprite.LayerMapTryGet(sprite, key, out var index, false))
                    _sprite.LayerSetVisible(sprite, index, false);
            }
        }
    }

    private void SetCorner(Entity<SpriteComponent?> sprite, int corner, Vector2 offset, string state)
    {
        // Glass and its frame, like the pieces themselves.
        var glass = SetLayer(sprite, CornerKey(corner), state, offset);
        SetLayer(sprite, CornerDetailsKey(corner), state + "_details", offset);
        _sprite.LayerSetColor(sprite, glass, GetGlassColor(sprite.Owner));
    }

    private int SetLayer(Entity<SpriteComponent?> sprite, string key, string state, Vector2 offset)
    {
        if (!_sprite.LayerMapTryGet(sprite, key, out var index, false))
        {
            index = _sprite.AddRsiLayer(sprite, state, _rsi);
            _sprite.LayerMapSet(sprite, key, index);
        }
        else
        {
            _sprite.LayerSetRsiState(sprite, index, state);
        }

        _sprite.LayerSetOffset(sprite, index, offset);
        _sprite.LayerSetVisible(sprite, index, true);
        return index;
    }

    /// <summary>
    /// Tints the glass layer, and the glass of corner pieces, with the tube's material.
    /// </summary>
    private void UpdateGlass(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComp))
            return;

        var sprite = new Entity<SpriteComponent?>(uid, spriteComp);
        var color = GetGlassColor(uid);

        if (_sprite.LayerMapTryGet(sprite, GlassKey, out var glass, false))
            _sprite.LayerSetColor(sprite, glass, color);

        for (var corner = 0; corner < MaxCorners; corner++)
        {
            if (_sprite.LayerMapTryGet(sprite, CornerKey(corner), out var index, false))
                _sprite.LayerSetColor(sprite, index, color);
        }
    }

    private Color GetGlassColor(EntityUid uid)
        => TryComp<TransitTubeGlassComponent>(uid, out var glass) && _proto.Resolve(glass.Material, out var material)
        ? material.Color
        : Color.White;

    private static string CornerKey(int corner) => $"transit-corner-{corner}";

    private static string CornerDetailsKey(int corner) => $"transit-corner-{corner}-details";

    private static string CornerState(int x, int y) => (x, y) switch
    {
        (> 0, > 0) => "corner_ne",
        (< 0, > 0) => "corner_nw",
        (> 0, _) => "corner_se",
        _ => "corner_sw",
    };
}
