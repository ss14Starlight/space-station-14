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

    public GameTick ServerAckTick { get; set; }

    public void Initialize()
    {
        _netManager.RegisterNetMessage<RedundantMovementMessage>(accept: NetMessageAccept.Server);
        _netManager.RegisterNetMessage<RedundantMovementAckMessage>(HandleMovementAckMessage, accept: NetMessageAccept.Client);
    }

    private void HandleMovementAckMessage(RedundantMovementAckMessage msg)
    {
        if (ServerAckTick < msg.Tick)
            ServerAckTick = msg.Tick;
    }

    public void SendTickData(GameTick tick, IEnumerable<TickInputData> data)
    {
        var msg = new RedundantMovementMessage()
        {
            SentTick = tick,
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

    private GameTick _lastSentTick = GameTick.Zero;
    private readonly Queue<TickInputData> _storedInputData = [];
    private readonly List<InputChange> _frameChanges = [];

    private GameTick? _sleepPeriodStart = null;

    public override void Initialize()
    {
        _manager.ServerAckTick = GameTick.Zero;

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
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<ClientRedundantMovementSystem>();
    }

    public override void Update(float frameTime)
    {
        if (!_cfg.GetCVar(RedundantMovementCVars.Enabled))
            return;

        var tick = _timing.CurTick;

        if (tick <= _lastSentTick)
            return;

        _lastSentTick = tick;

        if (!_netManager.IsConnected)
            return;

        // sleep logic
        bool validForSleep = !_currentState.HasInput && _frameChanges.Count == 0;

        if (!validForSleep)
        {
            _sleepPeriodStart = null;
        }
        else if (!_sleepPeriodStart.HasValue)
        {
            _sleepPeriodStart = tick;
        }

        if (_sleepPeriodStart.HasValue && _sleepPeriodStart.Value <= _manager.ServerAckTick)
        {
            _frameChanges.Clear();
            _storedInputData.Clear();
            return;
        }

        var thisTickInput = new TickInputData(tick, _currentState, _frameChanges.ToArray());
        _frameChanges.Clear();

        _storedInputData.Enqueue(thisTickInput);

        // enforce the max queue size
        int maxSize = _cfg.GetCVar(RedundantMovementCVars.MaxHistoryTicks);
        maxSize = int.Clamp(maxSize, 1, 64);
        while (_storedInputData.Count > maxSize) _storedInputData.Dequeue();

        // remove all packets that were acknowledged by the server
        // they no longer need to be sent
        var serverAckTick = _manager.ServerAckTick;
        while (_storedInputData.TryPeek(out var data))
        {
            if (data.Tick > serverAckTick) break;
            _storedInputData.Dequeue();
        }

        _manager.SendTickData(tick, _storedInputData);
    }

    private bool IsPilot(ICommonSession? session)
    {
        var uid = session?.AttachedEntity;
        return uid != null && TryComp<PilotComponent>(uid, out var pilot) && pilot.Console != null;
    }

    private void OnInputChange(PackedMovementButtons newInput, ushort subtick)
    {
        if (_currentState != newInput)
        {
            _currentState = newInput;
            _frameChanges.Add(new(subtick, newInput));
        }
    }

    private void OnInputEvent(ICommonSession? session, MoveButtons bit, bool pressed, ushort subtick)
    {
        if (_input.Predicted) return;

        var state = _movementState;
        if (pressed) state |= bit;
        else state &= ~bit;
        _movementState = state;

        if (!IsPilot(session)) OnInputChange(new(state), subtick);
    }

    private void OnInputEvent(ICommonSession? session, ShuttleButtons bit, bool pressed, ushort subtick)
    {
        if (_input.Predicted) return;

        var state = _shuttleState;
        if (pressed) state |= bit;
        else state &= ~bit;
        _shuttleState = state;

        if (IsPilot(session)) OnInputChange(new(state), subtick);
    }

    private sealed class MovementInputHandler(ClientRedundantMovementSystem system, MoveButtons bit) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.OnInputEvent(session, bit, message.State == BoundKeyState.Down, message.SubTick);
            return false;
        }
    }

    private sealed class ShuttleInputCmdHandler(ClientRedundantMovementSystem system, ShuttleButtons bit) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.OnInputEvent(session, bit, message.State == BoundKeyState.Down, message.SubTick);
            return false;
        }
    }
}
