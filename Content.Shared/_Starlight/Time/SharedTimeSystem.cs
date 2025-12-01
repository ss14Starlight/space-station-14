using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Time
{
    public sealed class TimeSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        
        private readonly DateTime _date = DateTime.UtcNow.AddYears(500);

        private TimeSpan _roundStart;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<TickerLobbyStatusEvent>(LobbyStatus);
        }

        private void LobbyStatus(TickerLobbyStatusEvent ev)
        {
            _roundStart = ev.RoundStartTimeSpan;
        }

        public (TimeSpan Time, string Date) GetStationTime()
        {
            var scaledTimeSinceStart = _timing.CurTime.Subtract(_roundStart).Multiply(4);
            var stationTime = scaledTimeSinceStart.Add(TimeSpan.FromHours(12));

            var totalDays = (int) stationTime.TotalDays;
            stationTime = stationTime.Subtract(TimeSpan.FromDays(totalDays));

            var newDate = _date.AddDays(totalDays);

            // ISO 8601 (YYYY-MM-DD or YYYYMMDD)
            return (stationTime, newDate.ToString("yyyy-MM-dd"));
        }
    }
}