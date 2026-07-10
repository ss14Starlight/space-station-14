using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Component to be inspected using the "Quick Inspect Component" keybind.
    /// Set by the "quickinspect" command.
    /// </summary>
    public static readonly CVarDef<string> DebugQuickInspect =
        CVarDef.Create("debug.quick_inspect", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    #region Starlight
    /// <summary>
    /// Experimental fix: rate-limits drunk overlay screen-texture copies to ~30Hz instead of every render frame.
    /// </summary>
    public static readonly CVarDef<bool> DrunkRenderFix =
        CVarDef.Create("debug.drunk_render_fix", false, CVar.CLIENTONLY | CVar.ARCHIVE);
    #endregion Starlight
}
