using System.Numerics;
using Content.Client.Clickable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Reflection;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// The character drawn layer by layer from its sprite, where each layer knows its body part:
/// clicking a part selects it, hovering one dims the rest of the body.
/// </summary>
public sealed class BodyPreviewControl : BoxContainer
{
    private static readonly RsiDirection[] Directions =
        [RsiDirection.South, RsiDirection.East, RsiDirection.North, RsiDirection.West];

    private readonly LayoutContainer _canvas = new() { HorizontalAlignment = HAlignment.Center };
    private readonly List<BodyPartPreviewControl> _layers = new();
    private readonly IClickMapManager _clickMap;
    private readonly IEntityManager _entity;
    private readonly IReflectionManager _reflection;

    private int _direction;

    public float Scale { get; set; } = 9;

    public RsiDirection Direction => Directions[_direction];

    public event Action<BodyEditorPart>? PartPressed;
    public event Action<BodyEditorPart?>? PartHovered;
    public event Action? Rotated;

    public BodyPreviewControl()
    {
        _clickMap = IoCManager.Resolve<IClickMapManager>();
        _entity = IoCManager.Resolve<IEntityManager>();
        _reflection = IoCManager.Resolve<IReflectionManager>();
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 4;

        var left = new Button { Text = "◀", StyleClasses = { "OpenRight" } };
        var right = new Button { Text = "▶", StyleClasses = { "OpenLeft" } };
        left.OnPressed += _ => Rotate(1);
        right.OnPressed += _ => Rotate(-1);

        AddChild(_canvas);
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            Children = { left, right },
        });
    }

    private void Rotate(int step)
    {
        _direction = (_direction + step + Directions.Length) % Directions.Length;
        Rotated?.Invoke();
    }

    /// <summary>
    /// Redraws the layers of <paramref name="entity"/>'s sprite, matching layers to parts through the backend.
    /// </summary>
    public void Update(EntityUid entity, IBodyEditorBackend backend)
    {
        _canvas.RemoveAllChildren();
        _layers.Clear();

        if (!_entity.TryGetComponent(entity, out SpriteComponent? sprite))
            return;

        var sprites = _entity.System<SpriteSystem>();
        var parts = new Dictionary<int, BodyEditorPart>();
        foreach (var (key, part) in backend.GetLayers())
        {
            var found = key switch
            {
                Enum layer => sprites.LayerMapTryGet((entity, sprite), layer, out var layerIndex, false) ? layerIndex : -1,
                string id => sprites.LayerMapTryGet((entity, sprite), id, out var idIndex, false) ? idIndex : -1,
                _ => -1,
            };
            if (found >= 0)
                parts.TryAdd(found, part);
        }

        var displacements = GetDisplacements((entity, sprite), sprites);

        var spriteScale = Vector2.Abs(sprite.Scale);
        var placed = new List<(BodyPartPreviewControl Control, Vector2 Position, Vector2 Size)>();
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            var i = index++;
            // Displacement maps live in their own layers and only feed a shader. The engine never draws them either.
            if (layer is not SpriteComponent.Layer { Visible: true, ActualRsi: { } rsi, CopyToShaderParameters: null } spriteLayer
                || !rsi.TryGetState(spriteLayer.State, out var state))
                continue;

            var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : Direction;
            var control = new BodyPartPreviewControl(parts.GetValueOrDefault(i), rsi, spriteLayer.State, direction,
                state.GetFrame(direction, 0), spriteLayer.Color, spriteScale * Scale, _clickMap, displacements.GetValueOrDefault(i));
            control.Pressed += part => PartPressed?.Invoke(part);
            control.OnMouseEntered += _ => PartHovered?.Invoke(control.Part);
            control.OnMouseExited += _ => PartHovered?.Invoke(null);

            var frameSize = control.SetSize / spriteScale;
            placed.Add((control, spriteLayer.Offset * new Vector2(1, -1) * EyeManager.PixelsPerMeter * Scale, frameSize));
            _canvas.AddChild(control);
            _layers.Add(control);
        }

        Place(placed, spriteScale);
    }

    private void Place(List<(BodyPartPreviewControl Control, Vector2 Position, Vector2 Size)> placed, Vector2 spriteScale)
    {
        if (placed.Count == 0)
        {
            _canvas.SetSize = Vector2.Zero;
            return;
        }

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var (_, position, size) in placed)
        {
            min = Vector2.Min(min, position);
            max = Vector2.Max(max, position + size);
        }

        var feet = new Vector2((min.X + max.X) / 2, max.Y);
        var canvas = Vector2.Max(max - min, (max - min) * spriteScale);
        var anchor = new Vector2(canvas.X / 2, canvas.Y);
        foreach (var (control, position, _) in placed)
            LayoutContainer.SetPosition(control, anchor + ((position - feet) * spriteScale));

        _canvas.SetSize = canvas;
    }

    /// <summary>
    /// Builds a displacement shader for every layer that the sprite displaces, keyed by layer index.
    /// It mirrors how the engine feeds displacement map layers into the shader of the layer they bend.
    /// </summary>
    private Dictionary<int, ShaderInstance> GetDisplacements(Entity<SpriteComponent> entity, SpriteSystem sprites)
    {
        var result = new Dictionary<int, ShaderInstance>();
        foreach (var layer in entity.Comp.AllLayers)
        {
            if (layer is not SpriteComponent.Layer { CopyToShaderParameters: { } parameters, ActualRsi: { } rsi } mapLayer
                || !rsi.TryGetState(mapLayer.State, out var state)
                || !TryGetLayerIndex(entity, sprites, parameters.LayerKey, out var target)
                || entity.Comp[target] is not SpriteComponent.Layer { Shader: { } shader })
                continue;

            var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : Direction;
            var map = state.GetFrame(direction, 0);
            var instance = shader.Duplicate();

            var source = map is AtlasTexture atlas ? atlas.SourceTexture : map;
            var region = map is AtlasTexture sub ? sub.SubRegion : UIBox2.FromDimensions(Vector2.Zero, map.Size);
            if (parameters.ParameterTexture is { } textureName)
                instance.SetParameter(textureName, source);
            if (parameters.ParameterUV is { } uvName)
            {
                // Same flip as the engine: textures are stored bottom row first.
                var (w, h) = (source.Width, (float) source.Height);
                instance.SetParameter(uvName, new Vector4(region.Left / w, (h - region.Bottom) / h, region.Right / w, (h - region.Top) / h));
            }

            result[target] = instance;
        }

        return result;
    }

    private bool TryGetLayerIndex(Entity<SpriteComponent> entity, SpriteSystem sprites, object key, out int index)
    {
        index = -1;
        return key switch
        {
            Enum layer => sprites.LayerMapTryGet(entity.AsNullable(), layer, out index, false),
            string id when sprites.LayerMapTryGet(entity.AsNullable(), id, out index, false) => true,
            string id when _reflection.TryParseEnumReference(id, out var layer) => sprites.LayerMapTryGet(entity.AsNullable(), layer, out index, false),
            _ => false,
        };
    }

    /// <summary>
    /// Dims every layer outside of <paramref name="part"/>, or none for null.
    /// </summary>
    public void Highlight(BodyEditorPart? part)
    {
        foreach (var layer in _layers)
            layer.Dimmed = part != null && !part.Contains(layer.Part);
    }
}
