using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared._Starlight.CCVar; // Starlight glass theme toggle
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Reflection;
using Timer = Robust.Shared.Timing.Timer;

#region Starlight
using Content.Client._Starlight;
using Content.Client._Daiquiri.Stylesheets; // Daiquiri
#endregion Starlight

namespace Content.Client.Stylesheets
{
    public sealed partial class StylesheetManager : IStylesheetManager
    {
        [Dependency] private ILogManager _logManager = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IReflectionManager _reflection = default!;
        [Dependency] private IConfigurationManager _cfg = default!; // Daiquiri glass theme toggle

        [Dependency]
        private IResourceCache
            _resCache = default!; // TODO: REMOVE (obsolete; used to construct StyleNano/StyleSpace)

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        public Stylesheet SheetGlassNanotrasen { get; private set; } = default!; // Daiquiri glass theme

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")] // Starlight
        public Stylesheet Starlight { get; private set; } = default!;  //🌟Starlight🌟

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        private int _glassBuildId;

        private bool _liquidGlassThemeActive;

        private Stylesheet RebuildGlass(out int glassified)
        {
            if (!_liquidGlassThemeActive)
            {
                glassified = 0;
                return SheetNanotrasen;
            }

            return GlassTheme.MakeGlass(SheetNanotrasen, GlassTheme.ReadLiquidParams(_cfg), out glassified);
        }

        /// <summary>
        /// </summary>
        private void DisableLegacyGlassSettings()
        {
            var changed = false;

            if (_cfg.GetCVar(StarlightCCVars.UIGlassTheme))
            {
                _cfg.SetCVar(StarlightCCVars.UIGlassTheme, false);
                changed = true;
            }

            if (_cfg.GetCVar(StarlightCCVars.UIGlassTransparencyEnabled))
            {
                _cfg.SetCVar(StarlightCCVars.UIGlassTransparencyEnabled, false);
                changed = true;
            }

            if (_cfg.GetCVar(StarlightCCVars.UIGlassAccentEnabled))
            {
                _cfg.SetCVar(StarlightCCVars.UIGlassAccentEnabled, false);
                changed = true;
            }

            if (changed)
                _cfg.SaveToFile();
        }

        private void RequestGlassRebuild()
        {
            var id = ++_glassBuildId;
            Timer.Spawn(120, () =>
            {
                if (id != _glassBuildId)
                    return;
                SheetGlassNanotrasen = RebuildGlass(out _);
                if (_liquidGlassThemeActive)
                    _userInterfaceManager.Stylesheet = SheetGlassNanotrasen;
            });
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            SheetNanotrasen = Init(new NanotrasenStylesheet(ResolveStyleTheme(), this)); // Starlight: themed
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetNano = new StyleNano(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            Starlight = new StyleStarlight(_resCache).Stylesheet; //🌟Starlight🌟 TODO: REMOVE (obsolete)
            _userInterfaceManager.Stylesheet = SheetNanotrasen;
            DisableLegacyGlassSettings();
            InitializeStyleTheme(); // Starlight
            SheetGlassNanotrasen = RebuildGlass(out var glassified);
            sawmill.Debug($"Glass theme variant: {glassified} panels glassified.");

            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassEnabled, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassBlur, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassRefraction, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassHighlight, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassOpacity, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTint, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTintEnabled, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassTintColor, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassCornerRadius, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassQuality, _ => RequestGlassRebuild());
            _cfg.OnValueChanged(StarlightCCVars.UILiquidGlassAnimate, _ => RequestGlassRebuild());

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
