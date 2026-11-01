using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlUiRangeSystem : EntitySystem
{
    private RemoteControlInterface _remoteControl = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _remoteControl = EntityManager.System<RemoteControlInterface>();
    }

    [SubscribeLocalEvent]
    private void OnCheckRange(ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (args.Result != BoundUserInterfaceRangeResult.Default
            || _remoteControl.ControlledEntity is not { } remoteEntity
            || args.UiKey is RemoteControlUIKey)
            return;

        if (_interaction.InRangeUnobstructed(remoteEntity, args.Target, args.Data.InteractionRange))
            args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    [SubscribeLocalEvent]
    private void OnRemoteUiAccessibleOverride(Entity<UserInterfaceComponent> entity, ref AccessibleOverrideEvent args)
    {
        if (args.Target != entity.Owner
            || args.User != _playerManager.LocalSession?.AttachedEntity)
            return;

        if (_remoteControl.ControlledEntity is not { } remoteEntity)
            return;

        if (args.Handled || args.Accessible)
            return;

        if (!TryComp(remoteEntity, out TransformComponent? remoteXform)
            || !TryComp(entity.Owner, out TransformComponent? uiXform))
            return;

        if (remoteXform.MapID == uiXform.MapID)
            return;

        var isOpenForUser = false;
        foreach (var actors in entity.Comp.Actors.Values)
        {
            if (!actors.Contains(args.User))
                continue;

            isOpenForUser = true;
            break;
        }

        if (!isOpenForUser)
            return;

        // The server still validates against the saved UI range; this only allows client prediction.
        args.Handled = true;
        args.Accessible = true;
    }
}
