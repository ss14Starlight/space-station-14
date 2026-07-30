using Content.Shared._Starlight.RedundantMovement;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.RedundantMovement;

public sealed partial class ClientRedundantMovementSystem : EntitySystem
{
    [Dependency] private InputSystem _input = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private MoveButtons _inputState = MoveButtons.None;
    private GameTick _lastSentTick = GameTick.Zero;
    private GameTick _serverAckTick = GameTick.Zero;
    private readonly Queue<TickInputData> _storedInputData = [];
    private readonly List<InputChange> _frameChanges = [];

    public override void Initialize()
    {
        _netManager.RegisterNetMessage<RedundantMovementMessage>(accept: NetMessageAccept.Server);
        _netManager.RegisterNetMessage<RedundantMovementAckMessage>(HandleMovementAckMessage, accept: NetMessageAccept.Client);
    
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.MoveUp, new MovementInputHandler(this, MoveButtons.Up))
            .Bind(EngineKeyFunctions.MoveDown, new MovementInputHandler(this, MoveButtons.Down))
            .Bind(EngineKeyFunctions.MoveLeft, new MovementInputHandler(this, MoveButtons.Left))
            .Bind(EngineKeyFunctions.MoveRight, new MovementInputHandler(this, MoveButtons.Right))
            .Bind(EngineKeyFunctions.Walk, new MovementInputHandler(this, MoveButtons.Walk))
            .Register<ClientRedundantMovementSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ClientRedundantMovementSystem>();
    }

    public override void Update(float frameTime)
    {
        var tick = _timing.CurTick;

        if (tick <= _lastSentTick)
            return;

        _lastSentTick = tick;

        var thisTickInput = new TickInputData(tick, _inputState, _frameChanges.ToArray());
        _frameChanges.Clear();

        _storedInputData.Enqueue(thisTickInput);

        // enforce the max queue size
        int maxSize = _cfg.GetCVar(RedundantMovementCVars.MaxHistoryTicks);
        while (_storedInputData.Count > maxSize) _storedInputData.Dequeue();

        // remove all packets that were acknowledged by the server
        // they no longer need to be sent
        while (_storedInputData.TryPeek(out var data))
        {
            if (data.Tick > _serverAckTick) break;
            _storedInputData.Dequeue();
        }

        var msg = new RedundantMovementMessage()
        {
            SentTick = tick,
        };

        msg.TickData.AddRange(_storedInputData);

        _netManager.ClientSendMessage(msg);
    }

    private void HandleMovementAckMessage(RedundantMovementAckMessage msg)
    {
        if (_serverAckTick < msg.Tick)
            _serverAckTick = msg.Tick;
    }

    private void OnInputChange(MoveButtons newInput, ushort subtick)
    {
        if (_input.Predicted) return;

        if (newInput != _inputState)
        {
            _inputState = newInput;
            _frameChanges.Add(new(subtick, newInput));
        }
    }

    private void OnInputEvent(MoveButtons bit, bool pressed, ushort subtick)
    {
        var state = _inputState;
        if (pressed) state |= bit;
        else state &= ~bit;
        OnInputChange(state, subtick);
    }

    private sealed class MovementInputHandler(ClientRedundantMovementSystem system, MoveButtons bit) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.OnInputEvent(bit, message.State == BoundKeyState.Down, message.SubTick);
            return false;
        }
    }
}
