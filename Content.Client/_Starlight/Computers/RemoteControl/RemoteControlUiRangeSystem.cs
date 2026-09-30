using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlUiRangeSystem : EntitySystem
{
    private RemoteControlInterface _remoteControl = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        _remoteControl = EntityManager.System<RemoteControlInterface>();
    }

    [SubscribeLocalEvent]
    private void OnCheckRange(ref BoundUserInterfaceCheckRangeEvent args)
    {
        if (_remoteControl.ControlledEntity is not { } remoteEntity || args.UiKey is RemoteControlUIKey)
            return;

        args.Result = _interaction.InRangeUnobstructed(remoteEntity, args.Target, args.Data.InteractionRange)
            ? BoundUserInterfaceRangeResult.Pass
            : BoundUserInterfaceRangeResult.Fail;
    }
}
