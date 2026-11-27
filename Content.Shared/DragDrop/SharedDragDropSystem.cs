using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;

namespace Content.Shared.DragDrop;

public abstract partial class SharedDragDropSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<DragDropRequestEvent>(OnDragDropRequestEvent);
    }

    private void OnDragDropRequestEvent(DragDropRequestEvent msg, EntitySessionEventArgs args)
    {
        var dragged = GetEntity(msg.Dragged);
        var target = GetEntity(msg.Target);

        if (Deleted(dragged) || Deleted(target))
            return;

        // Starlight - start
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        TryPerformDragDrop(user, dragged, target);
        // Starlight - end
    }
}
