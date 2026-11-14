using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Number of snapshot payloads a player may be sent per <see cref="SnapshotViewRateLimitPeriod"/>.
    /// Each payload is a full <see cref="Content.Shared._Starlight.Snapshots.SnapshotData"/>, so the limit caps
    /// bandwidth from repeated view/pickup/news-photo requests while allowing normal opening bursts.
    /// Shared by ServerSnapshotViewSystem and NewsSystem so tuning happens in one place.
    /// </summary>
    public static readonly CVarDef<int> SnapshotViewRateLimitCount =
        CVarDef.Create("snapshot.view_rate_limit_count", 2, CVar.SERVERONLY);

    /// <summary>
    /// Period, in seconds, over which snapshot payloads are counted. Short so a blocked player can retry
    /// quickly; the client retries every 0.2 s, so 2 per second throttles sustained spam without locking out normal use.
    /// </summary>
    public static readonly CVarDef<float> SnapshotViewRateLimitPeriod =
        CVarDef.Create("snapshot.view_rate_limit_period", 1f, CVar.SERVERONLY);
}
