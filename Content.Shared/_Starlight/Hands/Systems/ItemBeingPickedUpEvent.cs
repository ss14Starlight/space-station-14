// ReSharper disable CheckNamespace
namespace Content.Shared.Hands.EntitySystems;

[ByRefEvent]
public record struct ItemBeingPickedUpEvent(EntityUid User, EntityUid Item)
{
    public bool Cancelled = false;
}
