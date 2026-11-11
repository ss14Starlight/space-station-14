using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.StationRadio.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class StationRadioReceiverWindow : FancyWindow
{
    private const string PowerIcon = "/Textures/_Starlight/Interface/StationRadio/icon_power.png";

    private static readonly TimeSpan _pendingTimeout = TimeSpan.FromSeconds(1.5);

    private static readonly Color _lightOn = Color.FromHex("#4fc257");
    private static readonly Color _lightOff = Color.FromHex("#3a1f22");
    private static readonly Color _panelColor = Color.FromHex("#2d313a");
    private static readonly Color _panelBorder = Color.FromHex("#16181d");

    private readonly IEntityManager _entMan = IoCManager.Resolve<IEntityManager>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();

    private readonly PixelVolumeScale _scale = new();
    private readonly PixelKnob _knob = new();
    private readonly StyleBoxFlat _light = new() { BackgroundColor = _lightOff };
    private readonly Label _level;

    private EntityUid _radio;
    private TimeSpan _pendingSince;

    public event Action? OnPower;
    public event Action<float, bool>? OnVolume;

    public StationRadioReceiverWindow()
    {
        Title = Loc.GetString("station-radio-receiver-title");
        MinSize = SetSize = new Vector2(500, 180);

        var power = new Button
        {
            MinSize = new Vector2(40, 34),
            ToolTip = Loc.GetString("station-radio-receiver-power"),
            StyleClasses = { StyleClass.ButtonOpenBoth },
            VerticalAlignment = VAlignment.Center,
        };
        power.AddChild(new TextureRect
        {
            Texture = IoCManager.Resolve<IResourceCache>().GetTexture(PowerIcon),
            TextureScale = new Vector2(2, 2),
            Stretch = TextureRect.StretchMode.KeepCentered,
        });
        power.OnPressed += _ => OnPower?.Invoke();

        var light = new PanelContainer
        {
            MinSize = new Vector2(10, 10),
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = _light,
        };

        var powerBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 6,
        };
        powerBox.AddChild(light);
        powerBox.AddChild(power);

        _level = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = Color.FromHex("#999696"),
            StyleClasses = { StyleStarlight.StyleClassLabelSmall },
        };

        var dial = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        _scale.HorizontalAlignment = HAlignment.Center;
        dial.AddChild(_scale);
        dial.AddChild(_level);

        _knob.VerticalAlignment = VAlignment.Center;
        _knob.ToolTip = Loc.GetString("station-radio-receiver-knob");
        _knob.OnValueChanged += k =>
        {
            _pendingSince = _timing.RealTime;
            OnVolume?.Invoke(k.Value, false);
        };
        _knob.OnReleased += k =>
        {
            _pendingSince = _timing.RealTime;
            OnVolume?.Invoke(k.Value, true);
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 14,
            Margin = new Thickness(10),
        };
        row.AddChild(powerBox);
        row.AddChild(dial);
        row.AddChild(_knob);

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = _panelColor,
                BorderColor = _panelBorder,
                BorderThickness = new Thickness(2),
            },
            Margin = new Thickness(6),
        };
        panel.AddChild(row);
        ContentsContainer.AddChild(panel);
    }

    public void SetRadio(EntityUid radio) => _radio = radio;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entMan.TryGetComponent<StationRadioReceiverComponent>(_radio, out var radio))
            return;

        var waiting = _knob.Grabbed || _timing.RealTime - _pendingSince < _pendingTimeout;
        if (!waiting || MathHelper.CloseTo(radio.Volume, _knob.Value, 0.005f))
            _knob.Value = radio.Volume;

        _scale.Value = _knob.Value;
        _scale.Lit = radio.Active;
        _light.BackgroundColor = radio.Active ? _lightOn : _lightOff;
        _level.Text = Loc.GetString("station-radio-mixer-level", ("level", (int) MathF.Round(_knob.Value * 100)));
    }
}
