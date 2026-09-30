using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

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

    private void CalculateNewSpriteLinear(Entity<MapGridComponent>? gridEntity, IconSmoothComponent smooth,
        Entity<SpriteComponent> sprite, TransformComponent xform, EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var connections = LinearConnections.None;

        if (gridEntity != null)
        {
            var gridUid = gridEntity.Value.Owner;
            var grid = gridEntity.Value.Comp;
            var pos = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var rotation = xform.LocalRotation.GetCardinalDir().ToAngle();
            var right = RotatedOffset(rotation, 1);

            if (MatchingLinearEntity(smooth, rotation, _mapSystem.GetAnchoredEntities(gridUid, grid, pos + right), smoothQuery))
                connections |= LinearConnections.Right;

            if (MatchingLinearEntity(smooth, rotation, _mapSystem.GetAnchoredEntities(gridUid, grid, pos - right), smoothQuery))
                connections |= LinearConnections.Left;
        }

        _sprite.LayerSetRsiState(sprite.AsNullable(), 0, ResolveLinearState(sprite, smooth, (int)connections));
    }

    private string ResolveLinearState(Entity<SpriteComponent> sprite, IconSmoothComponent smooth, int index)
    {
        var suffix = smooth.StateSuffix;
        string[] candidates =
        [
            $"{smooth.StateBase}{index}{suffix}",
                $"{smooth.StateBase}{index}",
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

    private static Vector2i RotatedOffset(Angle rotation, int tiles)
    {
        var vec = rotation.RotateVec(new Vector2(tiles, 0));
        return new Vector2i((int)MathF.Round(vec.X), (int)MathF.Round(vec.Y));
    }

    private bool MatchingLinearEntity(IconSmoothComponent smooth, Angle rotation, AnchoredEntitiesEnumerator candidates,
        EntityQuery<IconSmoothComponent> smoothQuery)
    {
        while (candidates.MoveNext(out var entity))
        {
            if (!smoothQuery.TryGetComponent(entity, out var other)
                || other.SmoothKey == null
                || !other.Enabled
                || (other.SmoothKey != smooth.SmoothKey && !smooth.AdditionalKeys.Contains(other.SmoothKey)))
            {
                continue;
            }

            if (Transform(entity.Value).LocalRotation.GetCardinalDir().ToAngle().EqualsApprox(rotation))
                return true;
        }

        return false;
    }
}
