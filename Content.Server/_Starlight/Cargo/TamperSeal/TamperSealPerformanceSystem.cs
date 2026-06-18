using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;

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

        SubscribeLocalEvent<TamperSealComponent, TamperSealUnsealedEvent>(OnTamperSealUnsealed);
        SubscribeLocalEvent<TamperSealComponent, TamperSealDestroyedEvent>(OnTamperSealDestroyed);
    }

    #region Events

    private void OnTamperSealUnsealed(EntityUid uid, TamperSealComponent seal, TamperSealUnsealedEvent args)
    {
        var tracker = GetPerformanceTracker(seal);
        RecordPerformance(tracker, true, args.TamperSeal.Value);
        ReassessPerformance(tracker);
    }

    private void OnTamperSealDestroyed(EntityUid uid, TamperSealComponent seal, TamperSealDestroyedEvent args)
    {
        var tracker = GetPerformanceTracker(seal);
        RecordPerformance(tracker, false, args.TamperSeal.Value);
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

        // State change from "Not failing" to "Failing"
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

        // State change from "Failing" to "Not failing"
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

    private TamperSealPerformanceComponent GetPerformanceTracker(TamperSealComponent seal)
    {
        if (!TryComp<TamperSealPerformanceComponent>(seal.RecipientStation, out var tracker))
        {
            tracker = AddComp<TamperSealPerformanceComponent>(seal.RecipientStation);
            tracker.StationId = seal.RecipientStation;
        }

        return tracker;
    }

    #endregion
}

