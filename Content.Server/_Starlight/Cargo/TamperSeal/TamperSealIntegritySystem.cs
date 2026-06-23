using System.Linq;
using Content.Server._Starlight.Cargo.TamperSeal.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Cargo.TamperSeal;

/// <summary>
/// Tracks tamper seal integrity performance metrics. These metrics are scoped to stations and are server-side only.
/// </summary>
public sealed partial class TamperSealIntegritySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealIntegrityBeaconComponent, TamperSealOpenedEvent>(OnTamperSealOpened);
        SubscribeLocalEvent<TamperSealIntegrityBeaconComponent, TamperSealDestroyedEvent>(OnTamperSealDestroyed);
    }

    #region Events

    private void OnTamperSealOpened(EntityUid uid, TamperSealIntegrityBeaconComponent comp, TamperSealOpenedEvent args)
    {
        var tracker = GetTracker(comp.StationId);
        RecordPerformance(tracker, true);
        ReassessPerformance(tracker);
        RemCompDeferred<TamperSealIntegrityBeaconComponent>(uid);
    }

    private void OnTamperSealDestroyed(EntityUid uid, TamperSealIntegrityBeaconComponent comp, TamperSealDestroyedEvent args)
    {
        var tracker = GetTracker(comp.StationId);
        RecordPerformance(tracker, false);
        ReassessPerformance(tracker);

        if (!args.EntityDestroyed)
            RemCompDeferred<TamperSealIntegrityBeaconComponent>(uid);
    }

    #endregion
    #region Internal

    /// <summary>
    /// Given a tracker, reassess the current delivery performance of the station.
    /// </summary>
    private void ReassessPerformance(TamperSealIntegrityTrackerComponent tracker)
    {
        // Before guards so we can read out the success fraction via VV.
        var successCount = tracker.Records.Count(x => x.Success);
        var successFraction = tracker.SuccessFraction = (float)successCount / tracker.Records.Count;

        if (!tracker.Enabled) return;
        if (tracker.Records.Count < tracker.MinRecords) return;

        var shouldSet = successFraction < tracker.FailureSetThreshold;
        var shouldClear = successFraction >= tracker.FailureClearThreshold;

        // If state should change from "Not failing" to "Failing".
        if (shouldSet && !tracker.Failure)
        {
            tracker.Failure = true;

            _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(tracker.StationId):station} tamper-seal integrity levels dropped below {tracker.FailureSetThreshold*100f}%. Dispatching announcement.");
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
            _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(tracker.StationId):station} tamper-seal integrity levels restored to at least {tracker.FailureClearThreshold*100f}%.");
            tracker.Failure = false;
        }
    }

    private void RecordPerformance(TamperSealIntegrityTrackerComponent tracker, bool success)
    {
        var record = new TamperSealResult(_timing.CurTime, success);
        if (tracker.Records.Count == tracker.MaxRecords)
            tracker.Records.Dequeue();
        tracker.Records.Enqueue(record);
    }

    private TamperSealIntegrityTrackerComponent GetTracker(EntityUid stationId)
    {
        if (TryComp<TamperSealIntegrityTrackerComponent>(stationId, out var tracker))
            return tracker;

        tracker = AddComp<TamperSealIntegrityTrackerComponent>(stationId);
        tracker.StationId = stationId;
        return tracker;
    }

    #endregion
}
