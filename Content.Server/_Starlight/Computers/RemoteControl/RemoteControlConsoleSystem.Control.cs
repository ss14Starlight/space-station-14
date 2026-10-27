using Content.Server._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Computers.RemoteControl;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    [Dependency] private BodyControlSystem _controlOwnership = default!;

    private bool HasCooperativeControl(EntityUid controller, EntityUid target)
        => IsCooperativeSource(controller) || IsCooperativeTarget(controller)
           || IsCooperativeSource(target) || IsCooperativeTarget(target);

    private bool IsCooperativeSource(EntityUid entity)
        => TryComp<BodyControlSourceComponent>(entity, out var source) && !source.ExternallyDriven;

    private bool IsCooperativeTarget(EntityUid entity)
        => TryComp<BodyControlTargetComponent>(entity, out var target) && !target.ExternallyDriven;

    [SubscribeLocalEvent]
    private void OnBodyControlStopping(ref BodyControlStoppingEvent args)
        => CloseControllerInterfaces(args.Controller);

    [SubscribeLocalEvent]
    private void OnControlLeaseReleased(Entity<RemoteControlConsoleComponent> ent, ref BodyControlProviderReleasedEvent args)
    {
        if (ent.Comp.Controller == args.Actor)
            SetController(ent, ent.Comp, null, args.Body);
    }
}
