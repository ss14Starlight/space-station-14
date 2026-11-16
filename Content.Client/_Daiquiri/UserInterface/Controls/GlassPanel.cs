using Content.Client._Daiquiri.Stylesheets;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Analyzers;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Daiquiri.UserInterface.Controls;

/// <summary>
/// Use in place of hardcoded PanelOverride boxes in XAML.
/// </summary>
[Virtual]
public partial class GlassPanel : PanelContainer
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private Color _glassColor = Color.Transparent;
    private Action<bool>? _onGlassBool;
    private Action<string>? _onGlassString;
    private Action<int>? _onGlassInt;
    private bool _hooked;

    /// Base (opaque) background color, settable from XAML.
    public Color GlassColor
    {
        get => _glassColor;
        set
        {
            _glassColor = value;
            UpdateBox();
        }
    }

    private float _glassContentMargin = -1f;

    /// <summary>
    /// Optional content margin applied to all sides (mirrors StyleBoxFlat ContentMargin*Override).
    /// </summary>
    public float GlassContentMargin
    {
        get => _glassContentMargin;
        set
        {
            _glassContentMargin = value;
            UpdateBox();
        }
    }

    public GlassPanel()
    {
        IoCManager.InjectDependencies(this);
        _onGlassBool = _ => UpdateBox();
        _onGlassString = _ => UpdateBox();
        _onGlassInt = _ => UpdateBox();
        Hook();
    }

    /// <summary>
    /// </summary>
    protected override void EnteredTree()
    {
        base.EnteredTree();
        Hook();
        UpdateBox();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        Unhook();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            Unhook();
    }

    private void Hook()
    {
        if (_hooked)
            return;

        _hooked = true;

        _cfg.OnValueChanged(StarlightCCVars.StyleTheme, _onGlassString!, true);
        _cfg.OnValueChanged(StarlightCCVars.StyleAccent, _onGlassString!);
        _cfg.OnValueChanged(StarlightCCVars.StyleCustomAccent, _onGlassBool!);

        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassEnabled, _onGlassBool!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassBlur, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassRefraction, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassHighlight, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassOpacity, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTint, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTintEnabled, _onGlassBool!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTintColor, _onGlassString!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassCornerRadius, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassQuality, _onGlassInt!);
        _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassAnimate, _onGlassBool!);
    }

    private void Unhook()
    {
        if (!_hooked)
            return;

        _hooked = false;
        _cfg.UnsubValueChanged(StarlightCCVars.StyleTheme, _onGlassString!);
        _cfg.UnsubValueChanged(StarlightCCVars.StyleAccent, _onGlassString!);
        _cfg.UnsubValueChanged(StarlightCCVars.StyleCustomAccent, _onGlassBool!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassEnabled, _onGlassBool!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassBlur, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassRefraction, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassHighlight, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassOpacity, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassTint, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassTintEnabled, _onGlassBool!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassTintColor, _onGlassString!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassCornerRadius, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassQuality, _onGlassInt!);
        _cfg.UnsubValueChanged(StarlightCCVars.UILiquidGlassAnimate, _onGlassBool!);
    }

    private void UpdateBox()
    {
        if (_cfg == null!)
            return;

        var pars = GlassTheme.ReadLiquidParams(_cfg);
        var useGlass = pars.Enabled && _glassColor.A >= 0.99f && IsLiquidGlassTheme();

        var box = useGlass
            ? new LiquidGlassStyleBox(new StyleBoxFlat(_glassColor), pars)
            : (StyleBox) new StyleBoxFlat(_glassColor);
        if (_glassContentMargin >= 0f)
        {
            box.ContentMarginTopOverride = _glassContentMargin;
            box.ContentMarginBottomOverride = _glassContentMargin;
            box.ContentMarginLeftOverride = _glassContentMargin;
            box.ContentMarginRightOverride = _glassContentMargin;
        }
        PanelOverride = box;
    }

    private bool IsLiquidGlassTheme()
    {
        return _prototype.TryIndex<StyleThemePrototype>(
                   _cfg.GetCVar(StarlightCCVars.StyleTheme), out var proto)
               && proto.LiquidGlass;
    }
}
