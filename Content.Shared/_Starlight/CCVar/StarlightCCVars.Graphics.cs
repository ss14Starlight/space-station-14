using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Experimental fix: rate-limits drunk overlay screen-texture copies to ~30Hz instead of every render frame.
    /// </summary>
    public static readonly CVarDef<bool> DrunkRenderFix =
        CVarDef.Create("opt.drunk_render_fix", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
