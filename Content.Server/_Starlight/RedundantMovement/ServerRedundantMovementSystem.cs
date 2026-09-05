using System;
using Content.Server._Starlight.Physics;
using Content.Shared._Starlight.RedundantMovement;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.RedundantMovement;

public sealed partial class ServerRedundantMovementManager : IServerRedundantMovementManager
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly Dictionary<ICommonSession, SessionTracker> _trackers = [];

    public void Initialize()
    {
        _netManager.RegisterNetMessage<RedundantMovementMessage>(HandleMovementMessage, accept: NetMessageAccept.Server);
        _netManager.RegisterNetMessage<RedundantMovementAckMessage>(accept: NetMessageAccept.Client);
        _netManager.Disconnect += OnDisconnect;
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        var channel = _playerManager.GetSessionByChannel(e.Channel);
        if (channel != null)
            _trackers.Remove(channel);
    }

    private void HandleMovementMessage(RedundantMovementMessage msg)
    {
        var channel = _playerManager.GetSessionByChannel(msg.MsgChannel);
        if (channel == null) return;
        if (!_trackers.TryGetValue(channel, out var tracker))
            _trackers.Add(channel, tracker = new());

        tracker.Ingest(msg.TickData);

        _netManager.ServerSendMessage(new RedundantMovementAckMessage() { Tick = msg.SentTick }, msg.MsgChannel);
    }

    public void ApplyInput(GameTick tick, SLMoverController mover)
    {
        if (!_cfg.GetCVar(RedundantMovementCVars.Enabled))
        {
            _trackers.Clear();
            return;
        }

        foreach (var (session, tracker) in _trackers)
        {
            if (!tracker.TryFetch(tick, out var data)) continue;
            var curMoveState = tracker.MoveState;
            var curShuttleState = tracker.ShuttleState;
            if (!session.AttachedEntity.HasValue) continue;
            var entity = session.AttachedEntity.Value;

            void EmitStateChange(PackedMovementButtons buttons, ushort subtick)
            {
                var move = buttons.MoveButtons;
                var shuttle = buttons.ShuttleButtons;

                if (move != curMoveState)
                {
                    var changedBits = move ^ curMoveState;
                    for (int i = 0; i < 5; i++)
                    {
                        var toCheck = (MoveButtons)(1 << i);
                        if ((changedBits & toCheck) != 0)
                        {
                            mover.OnMoveButtonChange(entity, toCheck, (toCheck & move) != 0, subtick);
                        }
                    }

                    curMoveState = move;
                }

                if (shuttle != curShuttleState)
                {
                    var changedBits = shuttle ^ curShuttleState;
                    for (int i = 0; i < 7; i++)
                    {
                        var toCheck = (ShuttleButtons)(1 << i);
                        if ((changedBits & toCheck) != 0)
                        {
                            mover.OnShuttleButtonChange(entity, toCheck, (toCheck & shuttle) != 0, subtick);
                        }
                    }

                    curShuttleState = shuttle;
                }
            }

            foreach (var change in data.Changes)
            {
                EmitStateChange(change.HeldButtons, change.Subtick);
            }

            EmitStateChange(data.FinalInput, ushort.MaxValue);
            tracker.MoveState = curMoveState;
            tracker.ShuttleState = curShuttleState;
        }
    }

    public sealed class SessionTracker
    {
        private readonly Queue<TickInputData> _queue = [];
        private GameTick _mostRecentTick;

        public MoveButtons MoveState { get; set; }
        public ShuttleButtons ShuttleState { get; set; }

        public void Ingest(List<TickInputData> list)
        {
            foreach (var data in list)
            {
                if (data.Tick > _mostRecentTick)
                {
                    _queue.Enqueue(data);
                    _mostRecentTick = data.Tick;
                }
            }
        }

        public bool TryFetch(GameTick tick, out TickInputData data)
        {
            // check the oldest packet we have
            while (_queue.TryPeek(out data))
            {
                if (data.Tick < tick)
                {
                    // if it's too old, discard it
                    _queue.Dequeue();
                }
                else if (data.Tick == tick)
                {
                    // if it's for our tick, remove and return it
                    _queue.Dequeue();
                    return true;
                }
                else
                {
                    // if it's too new, then so are the rest
                    data = default;
                    return false;
                }
            }

            data = default;
            return false;
        }
    }
}

public sealed partial class ServerRedundantMovementSystem : EntitySystem
{
    [Dependency] private IServerRedundantMovementManager _manager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SLMoverController _mover = default!;

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(SLMoverController));
    }

    public override void Update(float frameTime)
    {
        _manager.ApplyInput(_timing.CurTick, _mover);
    }
}
