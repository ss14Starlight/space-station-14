using System.Linq;
using Content.Client.Options.UI;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Options.UI;

/// <summary>
/// A control that allows the user to pick a style theme from a list of available themes, displaying a preview of each theme's colors and name.
/// </summary>
public sealed class StyleThemePicker : GridContainer
{
    public const string StyleClassSwatchButton = "SwatchButton";

    private readonly Dictionary<string, ContainerButton> _cards = new();
    private readonly ButtonGroup _group = new();

    public event Action? OnPicked;

    public string? Selected { get; private set; }

    public StyleThemePicker()
    {
        Columns = 6;
        HSeparationOverride = 6;
        VSeparationOverride = 6;

        var proto = IoCManager.Resolve<IPrototypeManager>();
        foreach (var theme in proto.EnumeratePrototypes<StyleThemePrototype>().OrderBy(t => t.Order))
        {
            var card = new ContainerButton { ToggleMode = true, Group = _group, ToolTip = Loc.GetString(theme.Name) };
            card.AddStyleClass(StyleClassSwatchButton);
            card.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        Children =
                        {
                            Swatch(theme.Stock ? Palettes.Navy.BackgroundDark : theme.Background, 22, 32),
                            Swatch(theme.Stock ? Palettes.Navy.Background : theme.SurfaceRaised, 22, 32),
                            Swatch(theme.Stock ? Palettes.Navy.HoveredElement : theme.ElementHovered, 22, 32),
                        },
                    },
                    new Label { Text = Loc.GetString(theme.Name), HorizontalAlignment = HAlignment.Center },
                },
            });

            var id = theme.ID;
            card.OnPressed += _ =>
            {
                Selected = id;
                OnPicked?.Invoke();
            };
            _cards[id] = card;
            AddChild(card);
        }
    }

    public void Select(string id)
    {
        Selected = id;
        if (_cards.TryGetValue(id, out var card))
            card.Pressed = true;
    }

    internal static PanelContainer Swatch(Color color, float width, float height)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
            MinSize = new(width, height),
        };
    }
}

/// <summary>
/// A control that allows the user to pick an accent color from a list of preset colors or a custom color using sliders, with an option to use the theme's own accent color instead of a custom one.
/// </summary>
public sealed class AccentPicker : BoxContainer
{
    private static readonly Color[] Presets =
    [
        Color.FromHex("#D4D4D8"),
        Color.FromHex("#5B8DEF"),
        Color.FromHex("#4CAF6A"),
        Color.FromHex("#C9A227"),
        Color.FromHex("#D9534F"),
        Color.FromHex("#9B6BD6"),
    ];

    private readonly ButtonGroup _group = new();
    private readonly List<(Color Color, ContainerButton Button)> _presets = new();
    private readonly Button _theme;
    private readonly Button _custom;
    private readonly ColorSelectorSliders _sliders;

    public event Action? OnPicked;

    public Color Selected { get; private set; }

    /// <summary>
    /// Indicates whether the user has chosen to use a custom accent color instead of the theme's default accent color.
    /// </summary>
    public bool UseCustom { get; private set; }

    public AccentPicker()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        _sliders = new ColorSelectorSliders { IsAlphaVisible = false, Visible = false };
        _sliders.OnColorChanged += color => Pick(color, fromPreset: false);

        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = Loc.GetString("ui-options-style-accent"), HorizontalExpand = true });

        _theme = new Button { Text = Loc.GetString("ui-options-style-accent-theme"), ToggleMode = true, Group = _group };
        _theme.OnPressed += _ =>
        {
            UseCustom = false;
            _sliders.Visible = false;
            OnPicked?.Invoke();
        };
        row.AddChild(_theme);
        foreach (var color in Presets)
        {
            var button = new ContainerButton { ToggleMode = true, Group = _group };
            button.AddStyleClass(StyleThemePicker.StyleClassSwatchButton);
            button.AddChild(StyleThemePicker.Swatch(color, 18, 18));
            button.OnPressed += _ => Pick(color, fromPreset: true);
            _presets.Add((color, button));
            row.AddChild(button);
        }

        _custom = new Button { Text = Loc.GetString("ui-options-style-accent-custom"), ToggleMode = true, Group = _group };
        _custom.OnPressed += _ => _sliders.Visible = true;
        row.AddChild(_custom);
        AddChild(row);
        AddChild(_sliders);
    }

    public void SetUseCustom(bool useCustom)
    {
        UseCustom = useCustom;
        if (useCustom)
        {
            Select(Selected);
            return;
        }

        _theme.Pressed = true;
        _sliders.Visible = false;
    }

    public void Select(Color color)
    {
        Selected = color;
        _sliders.Color = color;
        if (!UseCustom)
            return;

        var preset = _presets.FirstOrDefault(p => p.Color.ToHex() == color.ToHex()).Button;
        if (preset != null)
        {
            preset.Pressed = true;
            _sliders.Visible = false;
        }
        else
        {
            _custom.Pressed = true;
            _sliders.Visible = true;
        }
    }

    private void Pick(Color color, bool fromPreset)
    {
        Selected = color;
        UseCustom = true;
        if (fromPreset)
        {
            _sliders.Color = color;
            _sliders.Visible = false;
        }

        OnPicked?.Invoke();
    }
}

public sealed class OptionStyleThemeCVar : BaseOptionCVar<string>
{
    private readonly StyleThemePicker _picker;

    protected override string Value
    {
        get => _picker.Selected ?? StarlightCCVars.StyleTheme.DefaultValue;
        set => _picker.Select(value);
    }

    public OptionStyleThemeCVar(OptionsTabControlRow controller, IConfigurationManager cfg, StyleThemePicker picker)
        : base(controller, cfg, StarlightCCVars.StyleTheme)
    {
        _picker = picker;
        picker.OnPicked += ValueChanged;
    }
}

public sealed class OptionAccentCVar : BaseOptionCVar<string>
{
    private readonly AccentPicker _picker;

    protected override string Value
    {
        get => _picker.Selected.ToHex();
        set => _picker.Select(Color.TryFromHex(value, out var color) ? color : Color.FromHex(StarlightCCVars.StyleAccent.DefaultValue));
    }

    public OptionAccentCVar(OptionsTabControlRow controller, IConfigurationManager cfg, AccentPicker picker)
        : base(controller, cfg, StarlightCCVars.StyleAccent)
    {
        _picker = picker;
        picker.OnPicked += ValueChanged;
    }

    protected override bool IsValueEqual(string a, string b)
    {
        return Color.TryFromHex(a, out var ca) && Color.TryFromHex(b, out var cb)
            ? ca.WithAlpha(1f).ToHex() == cb.WithAlpha(1f).ToHex()
            : a == b;
    }
}

public sealed class OptionCustomAccentCVar : BaseOptionCVar<bool>
{
    private readonly AccentPicker _picker;

    protected override bool Value
    {
        get => _picker.UseCustom;
        set => _picker.SetUseCustom(value);
    }

    public OptionCustomAccentCVar(OptionsTabControlRow controller, IConfigurationManager cfg, AccentPicker picker)
        : base(controller, cfg, StarlightCCVars.StyleCustomAccent)
    {
        _picker = picker;
        picker.OnPicked += ValueChanged;
    }
}
