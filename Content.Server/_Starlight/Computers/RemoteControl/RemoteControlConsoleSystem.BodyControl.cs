using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.UserInterface;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    [Dependency] private BodyControlHandContextSystem _controlHandContext = default!;

    private bool TryGetUiController(EntityUid body, out EntityUid provider, out EntityUid controller)
    {
        provider = default;
        controller = default;
        // A shared body can have several operators; a synchronous command identifies the actual requester.
        if (_controlHandContext.TryGetContext(body, out var context) && context.Controller != body
            && context.Access.Allows("body.inventory")
            && _controlOwnership.TryGetControl(context.Controller, out var controlled, out _) && controlled == body)
        {
            provider = controller = context.Controller;
            return true;
        }
        if (TryFindConsole(body, out var console, out controller))
        {
            provider = console;
            return true;
        }
        if (TryComp<BodyControlTargetComponent>(body, out var target) && target.ControllerCount == 1
            && target.SoleController is { } sole && sole != body
            && _controlOwnership.TryGetControl(sole, out var remote, out var access) && remote == body && access.Allows("body.inventory"))
        {
            provider = controller = sole;
            return true;
        }
        return false;
    }

    private bool IsCooperativeController(EntityUid actor)
        => TryComp<BodyControlSourceComponent>(actor, out var source) && !source.ExternallyDriven
           && source.Body != actor;

    private bool CheckControlPermission(EntityUid actor, string permission, bool notify = true)
    {
        if (_controlHandContext.TryGetContext(actor, out var context))
            actor = context.Controller;
        if (!TryComp<BodyControlSourceComponent>(actor, out var source) || source.ExternallyDriven
            || _controlOwnership.TryGetControl(actor, out _, out var access) && access.Allows(permission))
            return true;
        if (notify)
        {
            var denied = new BodyControlDeniedEvent(permission[5..]);
            RaiseLocalEvent(actor, ref denied);
        }
        return false;
    }

    private void RelayCooperativeUi(BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled || args.Message is OpenBoundInterfaceMessage or CloseBoundInterfaceMessage
            || !IsCooperativeController(args.Actor)
            || !_remoteUiRangeOverrideActors.ContainsKey((args.Target, args.UiKey, args.Actor))
            || !_controlOwnership.TryGetControl(args.Actor, out var body, out var access))
            return;
        args.Cancel();
        if (!CanUseCooperativeUi(args.Actor, args.Target, args.UiKey))
            return;

        // The engine has checked the real subscriber. Reapply content validation to the physical user.
        _controlHandContext.Execute(body, args.Actor, access, Comp<BodyControlSourceComponent>(args.Actor).SelectedHand, () =>
        {
            var message = args.Message;
            var previousActor = message.Actor;
            var previousEntity = message.Entity;
            var previousKey = message.UiKey;
            try
            {
                message.Actor = body;
                message.Entity = GetNetEntity(args.Target);
                message.UiKey = args.UiKey;
                var validation = new BoundUserInterfaceMessageAttempt(body, args.Target, args.UiKey, message);
                RaiseLocalEvent(validation);
                if (validation.Cancelled)
                    return;
                RaiseLocalEvent(args.Target, validation);
                if (!validation.Cancelled)
                    RaiseLocalEvent(args.Target, (object)message, broadcast: true);
            }
            finally
            {
                message.Actor = previousActor;
                message.Entity = previousEntity;
                message.UiKey = previousKey;
            }
        });
    }

    private void ForgetCooperativeUi(EntityUid uiEntity, Enum uiKey, EntityUid actor)
    {
        _pendingRemoteUiMirrors.Remove((uiEntity, uiKey, actor));
        _remoteUiMirrors.RemoveWhere(mirror => mirror.Console == actor && mirror.Controller == actor
            && mirror.UiEntity == uiEntity && mirror.UiKey.Equals(uiKey));
        _remoteStorageUiActors.Remove(actor);
        UntrackRemoteUiRangeOverrideActor(uiEntity, uiKey, actor);
    }

    /// <summary>Reuses the UI relay's cleanup without closing another player's interface on the physical body.</summary>
    public void CloseControllerInterfaces(EntityUid actor)
    {
        foreach (var mirror in _remoteUiMirrors.Where(m => m.Console == actor && m.Controller == actor).ToArray())
        {
            ForgetCooperativeUi(mirror.UiEntity, mirror.UiKey, actor);
            if (!TerminatingOrDeleted(mirror.UiEntity))
                _ui.CloseUi(mirror.UiEntity, mirror.UiKey, actor);
        }
    }

    private bool CanUseCooperativeUi(EntityUid actor, EntityUid uiEntity, Enum key)
    {
        if (!_controlOwnership.TryGetControl(actor, out var body, out var access) || !access.Allows("body.inventory")
            || !CanRemoteControlledEntityAct(body)
            || !_remoteUiRangeOverrideActors.ContainsKey((uiEntity, key, actor))
            || !_ui.TryGetInterfaceData((uiEntity, null), key, out var data))
            return false;
        var range = _remoteUiInteractionRangeOverrides.TryGetValue((uiEntity, key), out var original)
            ? original.OriginalData.InteractionRange : data.InteractionRange;
        return range <= 0 || _interaction.InRangeUnobstructed(body, uiEntity, range);
    }
}
