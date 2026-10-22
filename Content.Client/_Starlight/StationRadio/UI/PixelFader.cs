using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class PixelFader : Control
{
    private const string TrackPath = "/Textures/_Starlight/Interface/StationRadio/fader_track.png";
    private const string CapPath = "/Textures/_Starlight/Interface/StationRadio/fader_cap.png";

    private const int SlotTop = 5;
    private const int SlotBottom = 66;
    private const float CapIndexRow = 4.5f;

    private const int PixelScale = 3;

    private const float WheelStep = 0.05f;

    private readonly Texture _track;
    private readonly Texture _cap;

    private float _value = 1f;

    public float Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0f, 1f);
    }

    public bool Grabbed { get; private set; }

    public bool Disabled { get; set; }

    public event Action<PixelFader>? OnValueChanged;

    public event Action<PixelFader>? OnReleased;

    public PixelFader()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _track = cache.GetTexture(TrackPath);
        _cap = cache.GetTexture(CapPath);
        MouseFilter = MouseFilterMode.Stop;
    }

    private float Scale => MathF.Max(1f, MathF.Round(PixelScale * UIScale));

    protected override Vector2 MeasureOverride(Vector2 availableSize) =>
        new Vector2(_track.Width, _track.Height) * PixelScale;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = Scale;
        var trackSize = new Vector2(_track.Width, _track.Height) * scale;
        var origin = new Vector2(
            MathF.Floor((PixelWidth - trackSize.X) / 2f),
            MathF.Floor((PixelHeight - trackSize.Y) / 2f));
        var tint = Disabled ? Color.Gray : Color.White;

        handle.DrawTextureRect(_track, UIBox2.FromDimensions(origin, trackSize), tint);

        var capSize = new Vector2(_cap.Width, _cap.Height) * scale;
        var capX = origin.X + MathF.Floor((_track.Width - _cap.Width) / 2f) * scale;
        var capY = origin.Y + MathF.Round(ValueToRow(_value) - CapIndexRow) * scale;
        handle.DrawTextureRect(_cap, UIBox2.FromDimensions(new Vector2(capX, capY), capSize), tint);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || Disabled)
            return;

        Grabbed = true;
        SetFromPixelY(args.RelativePixelPosition.Y);
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

        if (Grabbed)
            SetFromPixelY(args.RelativePixelPosition.Y);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (Disabled || args.Delta.Y == 0)
            return;

        Value += MathF.Sign(args.Delta.Y) * WheelStep;
        OnValueChanged?.Invoke(this);
        OnReleased?.Invoke(this);
        args.Handle();
    }

    private void SetFromPixelY(float pixelY)
    {
        var scale = Scale;
        var originY = MathF.Floor((PixelHeight - _track.Height * scale) / 2f);
        var row = (pixelY - originY) / scale;
        var value = 1f - (row - SlotTop) / (SlotBottom - SlotTop);

        var old = _value;
        Value = value;
        if (!MathHelper.CloseTo(old, _value))
            OnValueChanged?.Invoke(this);
    }

    private static float ValueToRow(float value) => SlotTop + (1f - value) * (SlotBottom - SlotTop);
}
