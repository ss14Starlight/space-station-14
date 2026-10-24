using System.Diagnostics.CodeAnalysis;
using Content.Shared.Tabletop.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Tabletop;

public abstract partial class SharedTabletopSystem
{
    protected bool CanDragWithoutTable(EntityUid playerEntity, EntityUid target,
        [NotNullWhen(true)] out TabletopDraggableComponent? draggable)
    {
        if (!CanDrag(playerEntity, target, out draggable) || !draggable.DraggableWithoutTabletop)
            return false;

        if (playerEntity == target && !draggable.AllowDragSelf)
            return false;

        if (!TryComp(target, out MetaDataComponent? meta)
            || meta.EntityLifeStage >= EntityLifeStage.Terminating
            || (meta.Flags & MetaDataFlags.InContainer) == MetaDataFlags.InContainer)
            return false;

        // No range check, world dragging is meant to work from anywhere.
        return ActionBlockerSystem.CanInteract(playerEntity, target);
    }
}
