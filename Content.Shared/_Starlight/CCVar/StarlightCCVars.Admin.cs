using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<string> AdminGhostScriptPath =
        CVarDef.Create("admin.admin_ghost_script_path", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> AdminArenaMapPath =
        CVarDef.Create("admin.admin_arena_map_path", string.Empty, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    public static readonly CVarDef<bool> AdminArenaMapInit =
        CVarDef.Create("admin.admin_arena_map_init", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
