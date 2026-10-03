using Content.Shared._Starlight.CCVar;
using System.Linq;
using Content.Shared._Starlight.RedundantMovement;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.RedundantMovement;

public sealed partial class ClientRedundantMovementManager : IClientRedundantMovementManager
{
    [Dependency] private INetManager _netManager = default!;

    public uint ServerAckSequence { get; set; }

    public void Initialize()
    {
        _netManager.RegisterNetMessage<RedundantMovementMessage>(accept: NetMessageAccept.Server);
        _netManager.RegisterNetMessage<RedundantMovementAckMessage>(HandleMovementAckMessage, accept: NetMessageAccept.Client);
    }

    private void HandleMovementAckMessage(RedundantMovementAckMessage msg)
    {
        if (ServerAckSequence < msg.Sequence)
            ServerAckSequence = msg.Sequence;
    }

    public void SendTickData(GameTick tick, uint sequence, IEnumerable<TickInputData> data)
    {
        var msg = new RedundantMovementMessage()
        {
            SentTick = tick,
            Sequence = sequence,
        };

        msg.TickData.AddRange(data);

        _netManager.ClientSendMessage(msg);
    }
}

public sealed partial class ClientRedundantMovementSystem : EntitySystem
{
    [Dependency] private InputSystem _input = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientRedundantMovementManager _manager = default!;

    private MoveButtons _movementState = MoveButtons.None;
    private ShuttleButtons _shuttleState = ShuttleButtons.None;
    private PackedMovementButtons _currentState = default;

    /// <summary>Highest tick we have produced input data for</summary>
    private GameTick _lastSentTick = GameTick.Zero;
    private uint _sequence;

    /// <summary>Unacknowledged ticks, ordered by tick</summary>
    private readonly List<StoredTick> _storedInputData = [];
    private readonly List<(GameTick Tick, InputChange Change)> _frameChanges = [];

    public override void Initialize()
    {
        _manager.ServerAckSequence = 0;

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.MoveUp, new MovementInputHandler(this, MoveButtons.Up))
            .Bind(EngineKeyFunctions.MoveDown, new MovementInputHandler(this, MoveButtons.Down))
            .Bind(EngineKeyFunctions.MoveLeft, new MovementInputHandler(this, MoveButtons.Left))
            .Bind(EngineKeyFunctions.MoveRight, new MovementInputHandler(this, MoveButtons.Right))
            .Bind(EngineKeyFunctions.Walk, new MovementInputHandler(this, MoveButtons.Walk))
            .Bind(ContentKeyFunctions.ShuttleStrafeUp, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeUp))
            .Bind(ContentKeyFunctions.ShuttleStrafeLeft, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeLeft))
            .Bind(ContentKeyFunctions.ShuttleStrafeRight, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeRight))
            .Bind(ContentKeyFunctions.ShuttleStrafeDown, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeDown))
            .Bind(ContentKeyFunctions.ShuttleRotateLeft, new ShuttleInputCmdHandler(this, ShuttleButtons.RotateLeft))
            .Bind(ContentKeyFunctions.ShuttleRotateRight, new ShuttleInputCmdHandler(this, ShuttleButtons.RotateRight))
            .Bind(ContentKeyFunctions.ShuttleBrake, new ShuttleInputCmdHandler(this, ShuttleButtons.Brake))
            .Register<ClientRedundantMovementSystem>();

        _netManager.Connected += OnConnected;
        _netManager.Disconnect += OnDisconnect;
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<ClientRedundantMovementSystem>();
        _netManager.Connected -= OnConnected;
        _netManager.Disconnect -= OnDisconnect;
    }

    public void SendPackets()
    {
        if (!_cfg.GetCVar(StarlightCCVars.RedundantMovementEnabled))
        {
            ClearState();
            return;
        }

        if (!_netManager.IsConnected)
        {
            ClearState();
            return;
        }

        var tick = _timing.CurTick;
        var sequence = ++_sequence;

        var hadChanges = _frameChanges.Count > 0;
        foreach (var (changeTick, change) in _frameChanges)
        {
            if (changeTick <= _lastSentTick)
                RewriteFrom(changeTick, change, sequence);
            else
            {
                var entry = GetOrAdd(changeTick, sequence);
                entry.Changes.Add(change);
                entry.FinalInput = change.HeldButtons;
            }
        }

        _frameChanges.Clear();

        if (tick > _lastSentTick)
        {
            // keep sending ticks while anything is held so they have redundancy; once idle and acked, go quiet
            if (hadChanges || _currentState.HasInput)
                GetOrAdd(tick, sequence).FinalInput = _currentState;

            _lastSentTick = tick;
        }

        // enforce the max queue size, dropping the oldest ticks, but never ones written just now (a rewrite can be wider)
        int maxSize = _cfg.GetCVar(StarlightCCVars.RedundantMovementMaxHistoryTicks);
        maxSize = int.Clamp(maxSize, 1, 64);
        while (_storedInputData.Count > maxSize && _storedInputData[0].Sequence != sequence)
            _storedInputData.RemoveAt(0);

        // the server has every tick whose last write went out in an acknowledged message
        var ackSequence = _manager.ServerAckSequence;
        _storedInputData.RemoveAll(data => data.Sequence <= ackSequence);

        if (_storedInputData.Count == 0)
            return;

        _manager.SendTickData(tick, sequence, _storedInputData.Select(data => data.ToData()));
    }

    private void RewriteFrom(GameTick changeTick, InputChange change, uint sequence)
    {
        // anything further back is far too late for the server, it'll take the newest tick as late input instead
        var first = changeTick.Value + 63 < _lastSentTick.Value ? new GameTick(_lastSentTick.Value - 63) : changeTick;

        GetOrAdd(first, sequence).Changes.Add(change);

        var input = change.HeldButtons;
        for (var t = first; t <= _lastSentTick; t += 1)
        {
            var entry = GetOrAdd(t, sequence);
            if (t != first)
                foreach (var laterChange in entry.Changes)
                    input = laterChange.HeldButtons;
            entry.FinalInput = input;
        }
    }

    private StoredTick GetOrAdd(GameTick tick, uint sequence)
    {
        var index = _storedInputData.FindLastIndex(data => data.Tick <= tick);
        if (index >= 0 && _storedInputData[index].Tick == tick)
        {
            var existing = _storedInputData[index];
            existing.Sequence = sequence;
            return existing;
        }

        // a new tick starts from where the tick before it ended
        var previous = index >= 0 ? _storedInputData[index].FinalInput : _currentState;
        var entry = new StoredTick(tick, sequence, previous);
        _storedInputData.Insert(index + 1, entry);
        return entry;
    }

    private bool IsPilot(ICommonSession? session)
    {
        var uid = session?.AttachedEntity;
        return uid != null && TryComp<PilotComponent>(uid, out var pilot) && pilot.Console != null;
    }

    private void OnInputChange(PackedMovementButtons newInput, GameTick tick, ushort subtick)
    {
        if (_currentState != newInput)
        {
            _currentState = newInput;
            _frameChanges.Add((tick, new(subtick, newInput)));
        }
    }

    private void OnInputEvent(ICommonSession? session, MoveButtons bit, bool pressed, GameTick tick, ushort subtick)
    {
        if (_input.Predicted) return;

        var state = _movementState;
        if (pressed) state |= bit;
        else state &= ~bit;
        _movementState = state;

        if (!IsPilot(session)) OnInputChange(new(state), tick, subtick);
    }

    private void OnInputEvent(ICommonSession? session, ShuttleButtons bit, bool pressed, GameTick tick, ushort subtick)
    {
        if (_input.Predicted) return;

        var state = _shuttleState;
        if (pressed) state |= bit;
        else state &= ~bit;
        _shuttleState = state;

        if (IsPilot(session)) OnInputChange(new(state), tick, subtick);
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e) => ClearState();
    private void OnConnected(object? sender, NetChannelArgs e)
    {
        ClearState();

        _lastSentTick = GameTick.Zero;
        _sequence = 0;
        _manager.ServerAckSequence = 0;
    }

    private void ClearState()
    {
        _frameChanges.Clear();
        _storedInputData.Clear();
    }

    private sealed class MovementInputHandler(ClientRedundantMovementSystem system, MoveButtons bit) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.OnInputEvent(session, bit, message.State == BoundKeyState.Down, message.Tick, message.SubTick);
            return false;
        }
    }

    private sealed class ShuttleInputCmdHandler(ClientRedundantMovementSystem system, ShuttleButtons bit) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.OnInputEvent(session, bit, message.State == BoundKeyState.Down, message.Tick, message.SubTick);
            return false;
        }
    }

    private sealed class StoredTick(GameTick tick, uint sequence, PackedMovementButtons finalInput)
    {
        public readonly GameTick Tick = tick;

        public uint Sequence = sequence;

        public PackedMovementButtons FinalInput = finalInput;
        public readonly List<InputChange> Changes = [];

        public TickInputData ToData() => new(Tick, FinalInput, Changes.ToArray());
    }
}
