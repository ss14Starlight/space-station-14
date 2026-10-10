using System.Linq;
using Content.Client._Starlight.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.UserInterface;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Client.Stylesheets;

public sealed partial class StylesheetManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private bool _styleThemeRebuildQueued;

    private StyleTheme? _hudStyleTheme;

    private Stylesheet? _hudSheet;

    private void InitializeStyleTheme()
    {
        _cfg.OnValueChanged(StarlightCCVars.StyleTheme, _ => QueueStyleThemeRebuild());
        _cfg.OnValueChanged(StarlightCCVars.StyleAccent, _ => QueueStyleThemeRebuild());
        _cfg.OnValueChanged(StarlightCCVars.StyleCustomAccent, _ => QueueStyleThemeRebuild());
        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<StyleThemePrototype>())
                QueueStyleThemeRebuild();
        };
        _userInterfaceManager.OnScreenChanged += args => ApplyHudSheet(args.New);

        RebuildHudSheet();
    }

    private object ResolveStyleTheme()
    {
        if (!_prototype.TryIndex<StyleThemePrototype>(_cfg.GetCVar(StarlightCCVars.StyleTheme), out var proto))
        {
            proto = _prototype.EnumeratePrototypes<StyleThemePrototype>().MinBy(p => p.Order);
            if (proto == null)
            {
                _hudStyleTheme = null;
                return new BaseStylesheet.NoConfig();
            }
        }

        if (proto.Stock)
        {
            _hudStyleTheme = null;
            return new BaseStylesheet.NoConfig();
        }

        _hudStyleTheme = proto.Hud is { } hud && _prototype.TryIndex(hud, out var hudProto)
            ? new StyleTheme(hudProto, ResolveAccent(hudProto))
            : null;

        return new StyleTheme(proto, ResolveAccent(proto));
    }

    private Color ResolveAccent(StyleThemePrototype proto)
    {
        if (_cfg.GetCVar(StarlightCCVars.StyleCustomAccent)
            && Color.TryFromHex(_cfg.GetCVar(StarlightCCVars.StyleAccent), out var custom))
            return custom;

        return proto.Accent ?? Color.FromHex(StarlightCCVars.StyleAccent.DefaultValue);
    }

    private void QueueStyleThemeRebuild()
    {
        if (_styleThemeRebuildQueued)
            return;

        _styleThemeRebuildQueued = true;
        _userInterfaceManager.DeferAction(RebuildStyleTheme);
    }

    private void RebuildStyleTheme()
    {
        _styleThemeRebuildQueued = false;

        var sheet = new NanotrasenStylesheet(ResolveStyleTheme(), this);
        Stylesheets[sheet.StylesheetName] = sheet.Stylesheet;
        SheetNanotrasen = sheet.Stylesheet;
        _userInterfaceManager.Stylesheet = SheetNanotrasen;
        RebuildHudSheet();
    }

    private void RebuildHudSheet()
    {
        _hudSheet = _hudStyleTheme is { } hudTheme
            ? new NanotrasenStylesheet(hudTheme, this).Stylesheet
            : null;
        ApplyHudSheet(_userInterfaceManager.ActiveScreen);
    }

    private void ApplyHudSheet(UIScreen? screen)
        => screen?.Stylesheet = _hudSheet;
}
