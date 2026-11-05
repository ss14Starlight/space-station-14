using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Number of snapshot view requests allowed per player during the rate limit period.
    /// </summary>
    public static readonly CVarDef<int> SnapshotViewRateLimitCount =
        CVarDef.Create("snapshot.view_rate_limit_count", 2, CVar.SERVERONLY);

    /// <summary>
    /// Period, in seconds, over which snapshot view requests are counted.
    /// </summary>
    public static readonly CVarDef<float> SnapshotViewRateLimitPeriod =
        CVarDef.Create("snapshot.view_rate_limit_period", 1f, CVar.SERVERONLY);
}
