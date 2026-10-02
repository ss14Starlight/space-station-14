using System.Numerics;
using Content.Client.Construction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Construction;

/// <summary>
/// Places every construction ghost in a template and builds the combined placement preview.
/// </summary>
public sealed partial class ConstructionTemplatePlacementHijack : PlacementHijack
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly ConstructionSystem _constructionSystem;
    private readonly ConstructionTemplate _template;

    /// <summary>
    /// Creates a placement hijack for the supplied construction template.
    /// </summary>
    public ConstructionTemplatePlacementHijack(ConstructionSystem constructionSystem, ConstructionTemplate template)
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _constructionSystem = constructionSystem;
        _template = template;
    }

    /// <inheritdoc />
    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        _constructionSystem.SpawnTemplate(_template, coordinates, Manager.Direction);
        return true;
    }

    /// <inheritdoc />
    public override bool HijackDeletion(EntityUid entity)
    {
        if (_entityManager.HasComponent<ConstructionGhostComponent>(entity))
            _constructionSystem.ClearGhost(entity.GetHashCode());

        return true;
    }

    /// <inheritdoc />
    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);

        manager.CurrentTextures = new List<IDirectionalTextureProvider>();

        if (manager.CurrentMode is { } mode)
        {
            mode.ValidPlaceColor = mode.ValidPlaceColor.WithAlpha(0.5f);
            mode.InvalidPlaceColor = mode.InvalidPlaceColor.WithAlpha(0.5f);
        }

        if (manager.CurrentPlacementOverlayEntity is not { } overlay
            || !_entityManager.TryGetComponent<SpriteComponent>(overlay, out var overlaySprite))
            return;

        overlaySprite.GranularLayersRendering = true;

        foreach (var entry in _template.Entries)
            AddPreviewLayers((overlay, overlaySprite), entry);
    }

    private void AddPreviewLayers(Entity<SpriteComponent> overlay, ConstructionTemplateEntry entry)
    {
        if (!_constructionSystem.TryGetRecipePrototype(entry.Recipe, out var targetProtoId)
            || !_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? proto))
        {
            return;
        }

        if (!proto.Components.ContainsKey("Sprite"))
        {
            foreach (var texture in _spriteSystem.GetPrototypeTextures(proto))
                AddPreviewLayer(overlay, entry, texture, null, null);

            return;
        }

        var dummy = _entityManager.SpawnEntity(targetProtoId, MapCoordinates.Nullspace);

        try
        {
            var sourceSprite = _entityManager.EnsureComponent<SpriteComponent>(dummy);
            _entityManager.System<AppearanceSystem>().OnChangeData(dummy, sourceSprite);

            foreach (var rawLayer in sourceSprite.AllLayers)
            {
                var layer = (SpriteComponent.Layer) rawLayer;

                if (!layer.Visible)
                    continue;

                IDirectionalTextureProvider? texture = layer.Texture;

                if (texture is null
                    && layer.State.IsValid
                    && layer.ActualRsi is { } rsi
                    && rsi.TryGetState(layer.State, out var state))
                {
                    texture = state;
                }

                if (texture is null)
                    continue;

                AddPreviewLayer(overlay, entry, texture, sourceSprite, layer);
            }
        }
        finally
        {
            _entityManager.DeleteEntity(dummy);
        }
    }

    private void AddPreviewLayer(
        Entity<SpriteComponent> overlay,
        ConstructionTemplateEntry entry,
        IDirectionalTextureProvider texture,
        SpriteComponent? sourceSprite,
        SpriteComponent.Layer? sourceLayer)
    {
        var index = texture is RSI.State state
            ? _spriteSystem.AddRsiLayer(overlay.AsNullable(), state.StateId, state.RSI)
            : _spriteSystem.AddTextureLayer(overlay.AsNullable(), texture.Default);

        var entryRotation = entry.Direction.ToAngle();
        var matrix = Matrix3Helpers.CreateTransform(entry.Offset, entryRotation);

        if (sourceSprite is not null && sourceLayer is not null)
        {
            matrix = Matrix3x2.Multiply(sourceLayer.LocalMatrix, sourceSprite.LocalMatrix);
            matrix = Matrix3x2.Multiply(matrix, Matrix3Helpers.CreateTransform(entry.Offset, entryRotation));

            _spriteSystem.LayerSetColor(overlay.AsNullable(), index, sourceSprite.Color * sourceLayer.Color);
        }

        var scaleX = new Vector2(matrix.M11, matrix.M12).Length();
        var scaleY = new Vector2(matrix.M21, matrix.M22).Length();

        if (matrix.GetDeterminant() < 0f)
            scaleY = -scaleY;

        var directional = texture is RSI.State { RsiDirections: RsiDirectionType.Dir4 };
        var rotation = matrix.Rotation() - (directional ? entryRotation : Angle.Zero);

        _spriteSystem.LayerSetOffset(overlay.AsNullable(), index, new Vector2(matrix.M31, matrix.M32));
        _spriteSystem.LayerSetRotation(overlay.AsNullable(), index, rotation);
        _spriteSystem.LayerSetScale(overlay.AsNullable(), index, new Vector2(scaleX, scaleY));

        var sourceDirectionOffset = sourceLayer?.DirOffset ?? SpriteComponent.DirectionOffset.None;
        _spriteSystem.LayerSetDirOffset(
            overlay.AsNullable(),
            index,
            directional
                ? CombineDirectionOffsets(sourceDirectionOffset, GetDirectionOffset(entry.Direction))
                : SpriteComponent.DirectionOffset.None);

        if (!_spriteSystem.TryGetLayer(overlay.AsNullable(), index, out var overlayLayer, true))
            return;

        overlayLayer.RenderingStrategy = sourceSprite switch
        {
            { NoRotation: true } => LayerRenderingStrategy.NoRotation,
            { SnapCardinals: true } => LayerRenderingStrategy.SnapToCardinals,
            _ => sourceLayer?.RenderingStrategy ?? LayerRenderingStrategy.Default,
        };
    }

    private static SpriteComponent.DirectionOffset GetDirectionOffset(Direction direction)
        => direction switch
        {
            Direction.South => SpriteComponent.DirectionOffset.None,
            Direction.East => SpriteComponent.DirectionOffset.CounterClockwise,
            Direction.North => SpriteComponent.DirectionOffset.Flip,
            Direction.West => SpriteComponent.DirectionOffset.Clockwise,
            _ => SpriteComponent.DirectionOffset.None,
        };

    private static SpriteComponent.DirectionOffset CombineDirectionOffsets(
        SpriteComponent.DirectionOffset first,
        SpriteComponent.DirectionOffset second)
    {
        var turns = (GetQuarterTurns(first) + GetQuarterTurns(second)) % 4;

        return turns switch
        {
            0 => SpriteComponent.DirectionOffset.None,
            1 => SpriteComponent.DirectionOffset.CounterClockwise,
            2 => SpriteComponent.DirectionOffset.Flip,
            _ => SpriteComponent.DirectionOffset.Clockwise,
        };
    }

    private static int GetQuarterTurns(SpriteComponent.DirectionOffset offset)
        => offset switch
        {
            SpriteComponent.DirectionOffset.None => 0,
            SpriteComponent.DirectionOffset.CounterClockwise => 1,
            SpriteComponent.DirectionOffset.Flip => 2,
            SpriteComponent.DirectionOffset.Clockwise => 3,
            _ => 0,
        };
}
