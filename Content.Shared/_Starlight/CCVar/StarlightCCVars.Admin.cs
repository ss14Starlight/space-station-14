using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<string> AdminGhostScriptPath =
        CVarDef.Create("admin.admin_ghost_script_path", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}
