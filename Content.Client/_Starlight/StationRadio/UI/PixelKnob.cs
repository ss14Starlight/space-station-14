using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelKnob : Control
{
    private const string TexturePath = "/Textures/_Starlight/Interface/StationRadio/radio_knob.png";

    private const int PixelScale = 3;

    private const float Sweep = 270f;

    private static readonly Vector2 Center = new(16f, 15.5f);
    private const float PointerInner = 4.5f;
    private const float PointerOuter = 6f;

    private const float DragRange = 200f;

    private const float WheelStep = 0.05f;

    private static readonly Color PointerColor = Color.FromHex("#f2efe9");

    private readonly Texture _texture;
    private float _value = 1f;
    private Vector2 _lastMouse;

    public float Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0f, 1f);
    }

    public bool Grabbed { get; private set; }

    public event Action<PixelKnob>? OnValueChanged;
    public event Action<PixelKnob>? OnReleased;

    public PixelKnob()
    {
        _texture = IoCManager.Resolve<IResourceCache>().GetTexture(TexturePath);
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
    }

    private float Scale => MathF.Max(1f, MathF.Round(PixelScale * UIScale));

    protected override Vector2 MeasureOverride(Vector2 availableSize) => _texture.Size * PixelScale;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = Scale;
        var size = (Vector2) _texture.Size * scale;
        var origin = ((PixelSize - size) / 2f).Floored();
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(origin, size));

        var angle = MathHelper.DegreesToRadians(-Sweep / 2f + Sweep * _value - 90f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        foreach (var radius in new[] { PointerInner, PointerOuter })
        {
            var pixel = (Center + direction * radius).Rounded();
            handle.DrawRect(UIBox2.FromDimensions(origin + pixel * scale, new Vector2(scale, scale)), PointerColor);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Grabbed = true;
        _lastMouse = args.PointerLocation.Position;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Grabbed)
            return;

        Grabbed = false;
        OnReleased?.Invoke(this);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!Grabbed)
            return;

        var position = args.GlobalPixelPosition.Position;
        var delta = (position.X - _lastMouse.X) - (position.Y - _lastMouse.Y);
        _lastMouse = position;
        Turn(delta / (DragRange * UIScale));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0)
            return;

        Turn(MathF.Sign(args.Delta.Y) * WheelStep);
        OnReleased?.Invoke(this);
        args.Handle();
    }

    private void Turn(float amount)
    {
        var old = _value;
        Value += amount;
        if (!MathHelper.CloseTo(old, _value))
            OnValueChanged?.Invoke(this);
    }
}
