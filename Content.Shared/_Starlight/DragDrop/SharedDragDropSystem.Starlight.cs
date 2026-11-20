using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

// ReSharper disable CheckNamespace
namespace Content.Shared.DragDrop;

public abstract partial class SharedDragDropSystem
{
    /// <summary>
    /// Attempts to drag <paramref name="dragged"/> onto <paramref name="target"/> after validating interaction access and range.
    /// </summary>
    /// <param name="user">The entity performing the drag-and-drop interaction.</param>
    /// <param name="dragged">The entity being dragged.</param>
    /// <param name="target">The entity being targeted.</param>
    /// <returns>
    /// <see langword="false"/> if either entity is deleted, the user cannot interact with the target, the dragged
    /// entity cancels its interaction attempt, or either entity is outside unobstructed interaction range.
    /// Otherwise, <see langword="true"/> is returned if the dragged event was handled or the target event was dispatched.
    /// A <see langword="true"/> return does not guarantee that a drop occurred.
    /// </returns>
    /// <remarks>
    /// The method raises <see cref="DragDropDraggedEvent"/> on the dragged entity first. If it is handled, the method
    /// returns without raising <see cref="DragDropTargetEvent"/>. Otherwise, it raises <see cref="DragDropTargetEvent"/>
    /// on the target and returns <see langword="true"/> even if no target handler is registered.
    /// </remarks>
    public bool TryPerformDragDrop(EntityUid user, EntityUid dragged, EntityUid target)
    {
        if (Deleted(dragged)
            || Deleted(target)
            || !_actionBlockerSystem.CanInteract(user, target)
            || !_interaction.InRangeUnobstructed(user, dragged, popup: true)
            || !_interaction.InRangeUnobstructed(user, target, popup: true))
            return false;

        var interactionAttempt = new GettingInteractedWithAttemptEvent(user, dragged);
        RaiseLocalEvent(dragged, ref interactionAttempt);

        if (interactionAttempt.Cancelled)
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
