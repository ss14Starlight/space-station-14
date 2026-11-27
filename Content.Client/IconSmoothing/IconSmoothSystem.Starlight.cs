using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    [Flags]
    private enum LinearConnections : byte
    {
        None = 0,
        Right = 1,
        Left = 2,
    }

    private enum LinearCorner : byte
    {
        None,
        Left,
        Right,
    }

    private void CalculateNewSpriteLinear(Entity<MapGridComponent>? gridEntity, IconSmoothComponent smooth,
        Entity<SpriteComponent> sprite, TransformComponent xform, EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var connections = LinearConnections.None;
        var corner = LinearCorner.None;

        if (gridEntity is { } grid)
        {
            var pos = _mapSystem.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);
            var rotation = CardinalRotation(xform);

            if (smooth.LinearCorners)
                corner = GetLinearCorner(grid, smooth, pos, rotation, smoothQuery);

            if (corner == LinearCorner.None)
            {
                var right = RotatedOffset(rotation, new Vector2(1, 0));

                if (LinearConnects(grid, smooth, pos, pos + right, rotation, smoothQuery))
                    connections |= LinearConnections.Right;

                if (LinearConnects(grid, smooth, pos, pos - right, rotation, smoothQuery))
                    connections |= LinearConnections.Left;
            }
        }

        var key = corner switch
        {
            LinearCorner.Left => "corner_left",
            LinearCorner.Right => "corner_right",
            _ => ((int) connections).ToString(),
        };

        _sprite.LayerSetRsiState(sprite.AsNullable(), 0, ResolveLinearState(sprite, smooth, key));
    }

    private string ResolveLinearState(Entity<SpriteComponent> sprite, IconSmoothComponent smooth, string key)
    {
        var suffix = smooth.StateSuffix;
        string[] candidates =
        [
            $"{smooth.StateBase}{key}{suffix}",
            $"{smooth.StateBase}{key}",
            $"{smooth.StateBase}0{suffix}",
            $"{smooth.StateBase}0",
        ];

        if (_sprite.LayerGetEffectiveRsi(sprite.AsNullable(), 0) is not { } rsi)
            return candidates[0];

        foreach (var candidate in candidates)
        {
            if (rsi.TryGetState(candidate, out _))
                return candidate;
        }

        return candidates[^1];
    }

    private bool LinearConnects(Entity<MapGridComponent> grid, IconSmoothComponent smooth, Vector2i pos, Vector2i other,
        Angle rotation, EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var candidates = _mapSystem.GetAnchoredEntities(grid.Owner, grid.Comp, other);
        while (candidates.MoveNext(out var entity))
        {
            if (!IsLinearMatch(smooth, entity.Value, smoothQuery, out var otherSmooth))
                continue;

            var otherRotation = CardinalRotation(Transform(entity.Value));
            if (otherRotation.EqualsApprox(rotation))
                return true;

            if (smooth.LinearCorners
                && other + RotatedOffset(otherRotation, new Vector2(0, -1)) == pos
                && GetLinearCorner(grid, otherSmooth, other, otherRotation, smoothQuery) != LinearCorner.None)
            {
                return true;
            }
        }

        return false;
    }

    private LinearCorner GetLinearCorner(Entity<MapGridComponent> grid, IconSmoothComponent smooth, Vector2i pos,
        Angle rotation, EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var front = RotatedOffset(rotation, new Vector2(0, -1));
        var candidates = _mapSystem.GetAnchoredEntities(grid.Owner, grid.Comp, pos + front);
        while (candidates.MoveNext(out var entity))
        {
            if (!IsLinearMatch(smooth, entity.Value, smoothQuery, out _))
                continue;

            var side = RotatedOffset(CardinalRotation(Transform(entity.Value)), new Vector2(0, -1));
            if (side == front || side == -front)
                continue;

            if (HasLinearPiece(grid, smooth, pos - side, rotation, smoothQuery))
                return LinearCorner.None;

            return side == RotatedOffset(rotation, new Vector2(1, 0)) ? LinearCorner.Right : LinearCorner.Left;
        }

        return LinearCorner.None;
    }

    private bool HasLinearPiece(Entity<MapGridComponent> grid, IconSmoothComponent smooth, Vector2i pos, Angle rotation,
        EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var candidates = _mapSystem.GetAnchoredEntities(grid.Owner, grid.Comp, pos);
        while (candidates.MoveNext(out var entity))
        {
            if (IsLinearMatch(smooth, entity.Value, smoothQuery, out _)
                && CardinalRotation(Transform(entity.Value)).EqualsApprox(rotation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLinearMatch(IconSmoothComponent smooth, EntityUid entity,
        EntityQuery<IconSmoothComponent> smoothQuery, [NotNullWhen(true)] out IconSmoothComponent? other)
        => smoothQuery.TryGetComponent(entity, out other)
            && other.SmoothKey != null
            && other.Enabled
            && (other.SmoothKey == smooth.SmoothKey || smooth.AdditionalKeys.Contains(other.SmoothKey));

    private static Angle CardinalRotation(TransformComponent xform)
        => xform.LocalRotation.GetCardinalDir().ToAngle();

    private static Vector2i RotatedOffset(Angle rotation, Vector2 offset)
    {
        var vec = rotation.RotateVec(offset);
        return new Vector2i((int) MathF.Round(vec.X), (int) MathF.Round(vec.Y));
    }
}
