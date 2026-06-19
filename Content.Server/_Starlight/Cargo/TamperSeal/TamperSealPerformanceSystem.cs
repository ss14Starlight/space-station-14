using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Cargo.TamperSeal;

/// <summary>
/// Tracks tamper seal integrity performance metrics. These metrics are scoped to stations and are server-side only.
/// </summary>
public sealed partial class TamperSealPerformanceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealValueComponent, TamperSealValueRewardedEvent>(OnTamperSealRewarded);
        SubscribeLocalEvent<TamperSealValueComponent, TamperSealValuePenalizedEvent>(OnTamperSealPenalized);
    }

    #region Events

    private void OnTamperSealRewarded(EntityUid uid, TamperSealValueComponent value, TamperSealValueRewardedEvent args)
    {
        var tracker = GetPerformanceTracker(value);
        RecordPerformance(tracker, true, value.Value);
        ReassessPerformance(tracker);
    }

    private void OnTamperSealPenalized(EntityUid uid, TamperSealValueComponent value,
        TamperSealValuePenalizedEvent args)
    {
        var tracker = GetPerformanceTracker(value);
        RecordPerformance(tracker, false, value.Value);
        ReassessPerformance(tracker);
    }

    #endregion
    #region Internal

    /// <summary>
    /// Given a tracker, reassess the current delivery performance of the station.
    /// </summary>
    private void ReassessPerformance(TamperSealPerformanceComponent tracker)
    {
        if (!tracker.JudgementEnabled) return;
        if (tracker.Records.Count < tracker.JudgementMinRecords) return;

        var successCount = tracker.Records.Count(x => x.Success);
        var successRate = (float)successCount / tracker.Records.Count;

        var shouldSet = successRate < tracker.FailureSetThreshold;
        var shouldClear = successRate >= tracker.FailureClearThreshold;

        // If state should change from "Not failing" to "Failing".
        if (shouldSet && !tracker.Failure)
        {
            tracker.Failure = true;
            _chat.DispatchStationAnnouncement(tracker.StationId,
                Loc.GetString("tamper-seal-performance-failure-message"),
                Loc.GetString("tamper-seal-performance-failure-sender"),
                true,
                tracker.FailureAnnounceSound,
                tracker.FailureAnnounceColor);
            return;
        }

        // If state should change from "Failing" to "Not failing".
        if (shouldClear && tracker.Failure)
        {
            // TODO: admin log?
            tracker.Failure = false;
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="tracker"></param>
    /// <param name="success"></param>
    /// <param name="value"></param>
    private void RecordPerformance(TamperSealPerformanceComponent tracker, bool success, int value)
    {
        var record = new TamperSealResult(_timing.CurTime, success, value);
        tracker.Records.Add(record);

        ExpungeOverflowedRecords(tracker);
        ExpungeOutdatedRecords(tracker);
    }

    private void ExpungeOverflowedRecords(TamperSealPerformanceComponent tracker)
    {
        var records = tracker.Records;
        if (records.Count <= tracker.MaxRecords)
            return;

        records.RemoveRange(0, tracker.MaxRecords - records.Count);
    }

    private void ExpungeOutdatedRecords(TamperSealPerformanceComponent tracker)
    {
        var removable = tracker.Records.Capacity - tracker.MinRecords;
        if (removable <= 0)
            return;

        for (var i = 0; i < removable; i++)
        {
            var record = tracker.Records[0];
            if (record.Time >= _timing.CurTime - tracker.RecordLifetime)
                break;

            tracker.Records.RemoveAt(0);
        }
    }

    private TamperSealPerformanceComponent GetPerformanceTracker(TamperSealValueComponent value)
    {
        if (TryComp<TamperSealPerformanceComponent>(value.StationId, out var tracker))
            return tracker;

        tracker = AddComp<TamperSealPerformanceComponent>(value.StationId);
        tracker.StationId = value.StationId;
        return tracker;
    }

    #endregion
}
