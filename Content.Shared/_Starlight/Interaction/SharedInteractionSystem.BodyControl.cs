using Content.Shared._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    // A mirrored interface validates the body while retaining the real message sender.
    private EntityUid GetControlledUiActor(Entity<UserInterfaceComponent> ent, BoundUserInterfaceMessageAttempt args)
    {
        if (TryComp<BodyControlSourceComponent>(args.Actor, out var control) && !control.ExternallyDriven
            && control.Access.FullControl && control.Body != args.Actor && !TerminatingOrDeleted(control.Body)
            && _ui.IsUiOpen(ent.AsNullable(), args.UiKey, args.Actor))
            return control.Body;
        return args.Actor;
    }
}
