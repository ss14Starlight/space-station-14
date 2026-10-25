using Content.Shared._Starlight.BodyControl;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;

namespace Content.Client._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    [Dependency] private IPlayerManager _controlPlayer = default!;
    [Dependency] private BodyControlHandContextSystem _handContext = default!;
    [Dependency] private BodyControlViewSystem _bodyView = default!;
    [Dependency] private RemoteControlInterface _remoteInterface = default!;

    private string? _shownControlHand;
    private EntityUid? _shownControlSource;
    private EntityUid? _shownControlBody;

    private bool HasLocalControl()
        => _bodyView.TryGetSource(out _, out _);

    private void InitializeInteractions()
    {
        _remoteInterface.BodyInteractionRequested += OnBodyInteractionRequested;
        var binds = CommandBinds.Builder;
        foreach (var (key, command) in ControlBindings())
            binds.BindBefore(key, new PointerInputCmdHandler((session, coordinates, target) =>
            {
                if (!HasLocalControl())
                    return false;
                if (key == EngineKeyFunctions.UseSecondary
                    && (!TryComp<BodyControlSourceComponent>(_controlPlayer.LocalEntity, out var secondary)
                        || !EntityManager.System<SharedCombatModeSystem>().IsInCombatMode(secondary.Body)))
                    return false;
                // Combat uses the native melee/gun input handlers, including held fire and secondary attacks.
                if (command == BodyControlCommand.Interact
                    && TryComp<BodyControlSourceComponent>(_controlPlayer.LocalEntity, out var control)
                    && EntityManager.System<SharedCombatModeSystem>().IsInCombatMode(control.Body)
                    && control.Access.Allows("body.interact")
                    && (!HasComp<HandsComponent>(control.Body) || control.SelectedHand is { } selected
                        && _handContext.AllowsHand(control.Body, selected, control.Access)))
                    return true;
                SendControlCommand(new(command, target: target.IsValid() ? GetNetEntity(target) : null,
                    coordinates: GetNetCoordinates(coordinates)));
                return true;
            }, outsidePrediction: true), typeof(SharedHandsSystem), typeof(SharedInteractionSystem));
        binds.Register<BodyControlSystem>();
    }

    private void ShutdownInteractions()
    {
        CommandBinds.Unregister<BodyControlSystem>();
        _remoteInterface.BodyInteractionRequested -= OnBodyInteractionRequested;
        _remoteInterface.SetBodyControlEntity(null);
    }

    private static (BoundKeyFunction, BodyControlCommand)[] ControlBindings() =>
    [
        (EngineKeyFunctions.Use, BodyControlCommand.Interact),
        (EngineKeyFunctions.UseSecondary, BodyControlCommand.Interact),
        (ContentKeyFunctions.AltActivateItemInWorld, BodyControlCommand.AltInteract),
        (ContentKeyFunctions.ActivateItemInWorld, BodyControlCommand.ActivateWorld),
        (ContentKeyFunctions.UseItemInHand, BodyControlCommand.UseHand),
        (ContentKeyFunctions.AltUseItemInHand, BodyControlCommand.AltUseHand),
        (ContentKeyFunctions.SwapHands, BodyControlCommand.CycleHand),
        (ContentKeyFunctions.SwapHandsReverse, BodyControlCommand.CycleHandReverse),
        (ContentKeyFunctions.Drop, BodyControlCommand.Drop),
        (ContentKeyFunctions.ThrowItemInHand, BodyControlCommand.Throw),
        (ContentKeyFunctions.TryPullObject, BodyControlCommand.Pull),
    ];

    public void SendControlCommand(BodyControlCommandEvent command)
    {
        if (HasLocalControl())
            RaiseNetworkEvent(command);
    }

    private void OnBodyInteractionRequested(EntityUid target, bool alt, RemoteControlInteractionAction action)
    {
        if (!HasLocalControl())
            return;
        var command = action switch
        {
            RemoteControlInteractionAction.TryPull => BodyControlCommand.Pull,
            RemoteControlInteractionAction.MovePulledObject => BodyControlCommand.MovePulledObject,
            RemoteControlInteractionAction.Throw => BodyControlCommand.Throw,
            RemoteControlInteractionAction.Drop => BodyControlCommand.Drop,
            RemoteControlInteractionAction.Shoot => BodyControlCommand.Shoot,
            _ => alt ? BodyControlCommand.AltInteract : BodyControlCommand.Interact,
        };
        SendControlCommand(new(command, target: GetNetEntity(target), coordinates: GetNetCoordinates(Transform(target).Coordinates)));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!HasLocalControl())
        {
            _remoteInterface.SetBodyControlEntity(null);
            if (_shownControlSource != null)
            {
                _shownControlSource = null;
                _shownControlHand = null;
                EntityManager.System<Content.Client.Hands.Systems.HandsSystem>().RefreshControlHand();
            }
        }
        else if (TryComp<BodyControlSourceComponent>(_controlPlayer.LocalEntity, out var source)
                 && (_shownControlSource != _controlPlayer.LocalEntity || _shownControlBody != source.Body || _shownControlHand != source.SelectedHand))
        {
            _shownControlSource = _controlPlayer.LocalEntity;
            _shownControlBody = source.Body;
            _shownControlHand = source.SelectedHand;
            EntityManager.System<Content.Client.Hands.Systems.HandsSystem>().RefreshControlHand();
        }
        if (_bodyView.TryGetSource(out _, out var view))
            _remoteInterface.SetBodyControlEntity(view.Body != _controlPlayer.LocalEntity ? view.Body : null);
    }
}
