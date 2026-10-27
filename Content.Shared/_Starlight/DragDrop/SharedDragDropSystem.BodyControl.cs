using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.DragDrop;

public abstract partial class SharedDragDropSystem
{
    private EntityUid? GetDragDropUser(EntitySessionEventArgs args)
        => args.SenderSession.AttachedEntity is { } actor
            ? EntityManager.System<BodyControlHandContextSystem>().GetControlledBody(actor) : null;

    private bool WithControlledDragDrop(EntitySessionEventArgs args, Action command)
    {
        if (args.SenderSession.AttachedEntity is not { } actor)
            return false;
        var control = EntityManager.System<BodyControlHandContextSystem>();
        return control.WithController(actor, "body.interact", HasComp<HandsComponent>(control.GetControlledBody(actor)), command);
    }
}
