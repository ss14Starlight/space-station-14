using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Silicons.Borgs;

/// <summary>
/// Keeps a deliberately stale position for every borg that broadcasts to robotics consoles.
/// </summary>
/// <seealso cref="BorgLocationTrackerComponent"/>
public sealed partial class BorgLocationTrackerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgTransponderComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BorgTransponderComponent> ent, ref MapInitEvent args)
    {
        var tracker = EnsureComp<BorgLocationTrackerComponent>(ent);
        tracker.PendingLocation = GetLocation(ent);
        tracker.NextSample = _timing.CurTime + tracker.SampleDelay;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BorgLocationTrackerComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            if (now < tracker.NextSample)
                continue;

            tracker.ReportedLocation = tracker.PendingLocation;
            tracker.PendingLocation = GetLocation(uid);
            tracker.NextSample = now + tracker.SampleDelay;
        }
    }

    /// <summary>
    /// Where the borg was one sample ago, or an empty string if it has not published one yet.
    /// </summary>
    public string GetReportedLocation(EntityUid uid) =>
        TryComp<BorgLocationTrackerComponent>(uid, out var tracker) ? tracker.ReportedLocation : string.Empty;

    private string GetLocation(EntityUid uid)
    {
        var tile = _transform.GetGridOrMapTilePosition(uid);
        return $"({tile.X}, {tile.Y})";
    }
}
