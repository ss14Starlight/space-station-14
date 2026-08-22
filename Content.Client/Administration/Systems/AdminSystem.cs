using System.Linq;
using Content.Shared.Administration;
using Content.Shared.Administration.Events;
using Content.Shared._Starlight.Administration.Events; // Starlight
using Content.Shared.GameTicking;
using Robust.Shared.Network;

namespace Content.Client.Administration.Systems
{
    public sealed partial class AdminSystem : EntitySystem
    {
        public event Action<List<PlayerInfo>>? PlayerListChanged;
        public event Action<StationEventsChangedEvent>? StationEventsChanged; // Starlight

        private Dictionary<NetUserId, PlayerInfo>? _playerList;
        public StationEventsChangedEvent? StationEventsSnapshot { get; private set; } // Starlight
        public IReadOnlyList<PlayerInfo> PlayerList
        {
            get
            {
                if (_playerList != null) return _playerList.Values.ToList();

                return new List<PlayerInfo>();
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            InitializeOverlay();
            SubscribeNetworkEvent<FullPlayerListEvent>(OnPlayerListChanged);
            SubscribeNetworkEvent<PlayerInfoChangedEvent>(OnPlayerInfoChanged);
            SubscribeNetworkEvent<StationEventsChangedEvent>(OnStationEventsChanged); // Starlight
        }

        public override void Shutdown()
        {
            base.Shutdown();
            ShutdownOverlay();
        }

        private void OnPlayerInfoChanged(PlayerInfoChangedEvent ev)
        {
            if(ev.PlayerInfo == null) return;

            if (_playerList == null) _playerList = new();

            _playerList[ev.PlayerInfo.SessionId] = ev.PlayerInfo;
            PlayerListChanged?.Invoke(_playerList.Values.ToList());
        }

        private void OnPlayerListChanged(FullPlayerListEvent msg)
        {
            _playerList = msg.PlayersInfo.ToDictionary(x => x.SessionId, x => x);
            PlayerListChanged?.Invoke(msg.PlayersInfo);
        }
        // Starlight-start
        /// <summary>
        /// Handles incoming station events state snapshots from the server.
        /// </summary>
        private void OnStationEventsChanged(StationEventsChangedEvent msg)
        {
            StationEventsSnapshot = msg;
            StationEventsChanged?.Invoke(msg);
        }

        /// <summary>
        /// Requests a fresh station events snapshot from the server.
        /// </summary>
        public void RequestStationEvents()
        {
            RaiseNetworkEvent(new RequestStationEventsEvent());
        }

        /// <summary>
        /// Sends an administration command for the station event queue or active event management.
        /// </summary>
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
        // Starlight-end
    }
}
