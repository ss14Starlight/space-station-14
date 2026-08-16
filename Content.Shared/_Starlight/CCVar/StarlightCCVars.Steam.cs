using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Steam oAuth
    /// </summary>

    public static readonly CVarDef<string> SteamBaseUrl =
        CVarDef.Create("steam.baseurl", "https://starlight.network/api/auth/steam", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SteamSecret =
        CVarDef.Create("steam.secret", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
