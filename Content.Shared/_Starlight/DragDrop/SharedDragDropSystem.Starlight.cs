using Content.Shared.Interaction;

// ReSharper disable CheckNamespace
namespace Content.Shared.DragDrop;

public abstract partial class SharedDragDropSystem
{
    public bool TryPerformDragDrop(EntityUid user, EntityUid dragged, EntityUid target)
    {
        if (Deleted(dragged)
            || Deleted(target)
            || !_actionBlockerSystem.CanInteract(user, target)
            || !_interaction.InRangeUnobstructed(user, dragged, popup: true)
            || !_interaction.InRangeUnobstructed(user, target, popup: true))
            return false;

        var dragArgs = new DragDropDraggedEvent(user, target);
        RaiseLocalEvent(dragged, ref dragArgs);

        if (dragArgs.Handled)
            return true;

        var dropArgs = new DragDropTargetEvent(user, dragged);
        RaiseLocalEvent(target, ref dropArgs);
        return true;
    }
}
