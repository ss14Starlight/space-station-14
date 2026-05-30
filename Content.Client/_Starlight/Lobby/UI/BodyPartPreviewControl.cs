// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Clickable;
using Content.Shared._Starlight.Body.Editor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

public sealed class BodyPartPreviewControl : Control
{
    private static readonly Color _hoverColor = Color.LimeGreen;
    private static readonly Color _dimColor = new(0.25f, 0.25f, 0.25f, 0.4f);

    private Texture _texture;
    private Color _color;
    private readonly float _scale;
    private readonly Vector2 _size;
    private readonly IClickMapManager _clickMap;
    private RsiDirection? _direction;
    private readonly bool _clickable;
    private bool _hovered;

    public bool Dimmed { get; set; }

    public BodyPartAddress Path { get; }
    public string LayerId { get; }

    public Color Color
    {
        get => _color;
        set => _color = value;
    }

    public SpriteSpecifier Sprite { get; }

    public void SetTexture(Texture texture, RsiDirection? direction)
    {
        _texture = texture;
        _direction = direction;
    }

    public event Action<BodyPartAddress, string>? Pressed;

    public BodyPartPreviewControl(BodyPartAddress path, string layerId, SpriteSpecifier sprite, Texture texture, Color color, float scale, IClickMapManager clickMap, RsiDirection? direction, bool clickable = true)
    {
        Path = path;
        LayerId = layerId;
        Sprite = sprite;
        _texture = texture;
        _color = color;
        _scale = scale;
        _clickMap = clickMap;
        _direction = direction;
        _clickable = clickable;

        _size = new Vector2(texture.Width, texture.Height) * scale;
        MouseFilter = clickable ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
        MinSize = _size;
        SetSize = _size;

        if (!clickable)
            return;

        OnMouseEntered += _ => _hovered = true;

        OnMouseExited += _ => _hovered = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        var color = _hovered ? _hoverColor : _color;
        if (Dimmed && !_hovered)
            color = new Color(color.R * _dimColor.R, color.G * _dimColor.G, color.B * _dimColor.B, color.A * _dimColor.A);
        handle.DrawTextureRect(_texture, rect, color);
    }

    protected override bool HasPoint(Vector2 point)
    {
        if (!_clickable)
            return false;

        if (point.X < 0 || point.Y < 0 || point.X >= _size.X || point.Y >= _size.Y)
            return false;

        var imagePos = (Vector2i) (point / _scale);

        return _clickMap.IsOccluding(Sprite, imagePos, 0, _direction);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (!_clickable)
            return;
        Pressed?.Invoke(Path, LayerId);
    }
}
