using System.Linq;
using Content.Server.StationEvents.Components;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.StationEvents;

public sealed partial class EventManagerSystem
{
    /// <summary>Counts how often a station event has run this round.</summary>
    public int GetStationEventOccurrences(EntityPrototype stationEvent) => GetOccurrences(stationEvent);

        /// <summary>Checks whether an ID belongs to a station event.</summary>
    public bool HasEvent(string eventId)
    {
        return AllEvents().Keys.Any(proto => proto.ID == eventId);
    }

        /// <summary>Runs a station event by ID.</summary>
    public bool RunEventById(string eventId)
    {
        if (!HasEvent(eventId))
            return false;

        return GameTicker.AddGameRule(eventId) != EntityUid.Invalid;
    }

        /// <summary>Gets the last run time, distinguishing a never-run event.</summary>
    public bool TryGetLastEventTime(EntityPrototype stationEvent, out TimeSpan lastRun)
    {
        foreach (var (time, rule) in GameTicker.AllPreviousGameRules.Reverse())
        {
            if (rule != stationEvent.ID)
                continue;

            lastRun = time;
            return true;
        }

        lastRun = TimeSpan.Zero;
        return false;
    }

        /// <summary>Checks whether an event currently meets its restrictions.</summary>
    public bool CanRunNow(string eventId)
    {
        foreach (var (proto, stationEvent) in AllEvents())
        {
            if (proto.ID != eventId)
                continue;

            return CanRun(proto, stationEvent, _playerManager.PlayerCount, GameTicker.RoundDuration());
        }

        return false;
    }
}
