using Content.Shared._Starlight.Administration.Events;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Client.Administration.Systems;

public sealed partial class AdminSystem
{
    public event Action<StationEventsChangedEvent>? StationEventsChanged;
    public StationEventsChangedEvent? StationEventsSnapshot { get; private set; }

        private void OnStationEventsChanged(StationEventsChangedEvent msg)
        {
            StationEventsSnapshot = msg;
            StationEventsChanged?.Invoke(msg);
        }

        /// <summary>Requests a station events snapshot.</summary>
        public void RequestStationEvents()
        {
            RaiseNetworkEvent(new RequestStationEventsEvent());
        }

        /// <summary>Sends an admin station event action.</summary>
        public void SendStationEventCommand(
            StationEventQueueCommand command,
            string eventId = "",
            int queueId = 0,
            float seconds = -1f,
            NetEntity activeEvent = default)
        {
            RaiseNetworkEvent(new StationEventQueueCommandEvent
            {
                Command = command,
                EventId = eventId,
                QueueId = queueId,
                Seconds = seconds,
                ActiveEvent = activeEvent
            });
        }
}
