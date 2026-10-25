using System.Numerics;
using Content.Client.Clickable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;

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

        var size = Vector2.Zero;
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            var i = index++;
            if (layer is not SpriteComponent.Layer { Visible: true, ActualRsi: { } rsi } spriteLayer
                || !rsi.TryGetState(spriteLayer.State, out var state))
                continue;

            var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : Direction;
            var control = new BodyPartPreviewControl(parts.GetValueOrDefault(i), rsi, spriteLayer.State, direction,
                state.GetFrame(direction, 0), spriteLayer.Color, Scale, _clickMap);
            control.Pressed += part => PartPressed?.Invoke(part);
            control.OnMouseEntered += _ => PartHovered?.Invoke(control.Part);
            control.OnMouseExited += _ => PartHovered?.Invoke(null);

            LayoutContainer.SetPosition(control, spriteLayer.Offset * new Vector2(1, -1) * EyeManager.PixelsPerMeter * Scale);
            size = Vector2.Max(size, control.SetSize);
            _canvas.AddChild(control);
            _layers.Add(control);
        }

        _canvas.SetSize = size;
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
