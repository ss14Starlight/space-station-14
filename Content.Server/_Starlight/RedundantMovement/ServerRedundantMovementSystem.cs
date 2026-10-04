using System;
using Content.Server._Starlight.Physics;
using Content.Shared._Starlight.CCVar;
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
        if (_playerManager.TryGetSessionByChannel(e.Channel, out var session) && session != null)
            _trackers.Remove(session);
    }

    private void HandleMovementMessage(RedundantMovementMessage msg)
    {
        if (!_playerManager.TryGetSessionByChannel(msg.MsgChannel, out var session) || session == null)
            return;

        if (!_trackers.TryGetValue(session, out var tracker))
            _trackers.Add(session, tracker = new());

        tracker.Ingest(msg.Sequence, msg.TickData);

        _netManager.ServerSendMessage(new RedundantMovementAckMessage() { Sequence = msg.Sequence }, msg.MsgChannel);
    }

    public void ApplyInput(GameTick tick, SLMoverController mover)
    {
        if (!_cfg.GetCVar(StarlightCCVars.RedundantMovementEnabled))
        {
            _trackers.Clear();
            return;
        }

        foreach (var (session, tracker) in _trackers)
        {
            if (!tracker.TryFetch(tick, out var lateInput, out var data)) continue;
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

            if (lateInput is { } late)
                EmitStateChange(late, 0);

            if (data is { } current)
            {
                foreach (var change in current.Changes)
                {
                    EmitStateChange(change.HeldButtons, change.Subtick);
                }

                EmitStateChange(current.FinalInput, ushort.MaxValue);
            }
            tracker.MoveState = curMoveState;
            tracker.ShuttleState = curShuttleState;
        }
    }

    public sealed class SessionTracker
    {
        private readonly SortedDictionary<GameTick, (TickInputData Data, uint Sequence)> _pending = [];
        private GameTick _lastAppliedTick;
        private uint _appliedSequence;

        private (uint Sequence, GameTick Tick, PackedMovementButtons Input)? _late;

        public MoveButtons MoveState { get; set; }
        public ShuttleButtons ShuttleState { get; set; }

        public void Ingest(uint sequence, List<TickInputData> list)
        {
            foreach (var data in list)
            {
                if (data.Tick <= _lastAppliedTick)
                {
                    ConsiderLate(sequence, data.Tick, data.FinalInput);
                    continue;
                }

                if (_pending.TryGetValue(data.Tick, out var existing) && existing.Sequence >= sequence)
                    continue;

                _pending[data.Tick] = (data, sequence);
            }
        }

        private void ConsiderLate(uint sequence, GameTick tick, PackedMovementButtons input)
        {
            // redundant copies of what we already applied carry nothing new
            if (sequence <= _appliedSequence)
                return;

            // every message covers the client's whole timeline, so the newest message's latest tick wins
            if (_late is { } late && (late.Sequence > sequence || late.Sequence == sequence && late.Tick >= tick))
                return;

            _late = (sequence, tick, input);
        }

        /// <summary>
        /// Fetches input for <paramref name="tick"/>. Input for ticks that already ran is collapsed into
        /// <paramref name="lateInput"/> (the held buttons it ended on) instead of being dropped.
        /// </summary>
        public bool TryFetch(GameTick tick, out PackedMovementButtons? lateInput, out TickInputData? current)
        {
            lateInput = null;
            current = null;

            // anything still queued for an earlier tick missed its slot
            while (_pending.Count > 0)
            {
                using var enumerator = _pending.GetEnumerator();
                enumerator.MoveNext();
                var (oldTick, (oldData, oldSequence)) = enumerator.Current;
                if (oldTick >= tick)
                    break;

                _pending.Remove(oldTick);
                ConsiderLate(oldSequence, oldTick, oldData.FinalInput);
            }

            if (_late is { } late)
            {
                lateInput = late.Input;
                _appliedSequence = late.Sequence;
                _late = null;
            }

            if (_pending.Remove(tick, out var entry))
            {
                current = entry.Data;
                _appliedSequence = Math.Max(_appliedSequence, entry.Sequence);
            }

            _lastAppliedTick = tick;
            return lateInput != null || current != null;
        }
    }
}

public sealed partial class ServerRedundantMovementSystem : EntitySystem
{
    [Dependency] private IServerRedundantMovementManager _manager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SLMoverController _mover = default!;

    public override void Initialize() => UpdatesBefore.Add(typeof(SLMoverController));

    public override void Update(float frameTime) => _manager.ApplyInput(_timing.CurTick, _mover);
}
