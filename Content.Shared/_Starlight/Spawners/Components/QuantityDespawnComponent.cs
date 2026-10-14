using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Spawners.Components;

/// <summary>
/// This goes on something where you only want a maximum number in existence.
/// Once the limit is exceeded, entities get deleted oldest first to make way for newly spawned ones.
/// </summary>
/// <remarks>
/// Similar to TimedDespawnComponent, this is not networked, as client will not have full view of
/// all entities, and therefore cannot know what ones are being deleted.
/// </remarks>
[RegisterComponent]
public sealed partial class QuantityDespawnComponent : Component
{
    [DataField]
    public EntProtoId<QuantityDespawnCategoryComponent> Category = default!;
}
