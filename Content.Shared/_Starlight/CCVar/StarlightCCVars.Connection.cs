using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> ConnRateLimitEnabled =
        CVarDef.Create("starlight.conn_ratelimit.enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> ConnRateLimitPerIpCount =
        CVarDef.Create("starlight.conn_ratelimit.per_ip_count", 20, CVar.SERVERONLY);

    public static readonly CVarDef<int> ConnRateLimitPerIpSeconds =
        CVarDef.Create("starlight.conn_ratelimit.per_ip_seconds", 60, CVar.SERVERONLY);

    public static readonly CVarDef<int> ConnRateLimitGlobalCount =
        CVarDef.Create("starlight.conn_ratelimit.global_count", 600, CVar.SERVERONLY);

    public static readonly CVarDef<int> ConnRateLimitGlobalSeconds =
        CVarDef.Create("starlight.conn_ratelimit.global_seconds", 10, CVar.SERVERONLY);
}
