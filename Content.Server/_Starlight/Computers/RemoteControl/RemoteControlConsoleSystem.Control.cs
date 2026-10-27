using Content.Shared._Starlight.BodyControl;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    private bool HasBodyControl(EntityUid controller, EntityUid target)
        => HasComp<BodyControlSourceComponent>(controller) || HasComp<BodyControlTargetComponent>(controller)
           || HasComp<BodyControlSourceComponent>(target) || HasComp<BodyControlTargetComponent>(target);
}
