using Content.Shared.Flash.Components;
using Content.Shared.Inventory;

namespace Content.Shared.Flash;

/// <summary>
/// Called before a flash is used to check if the attempt is cancelled by blindness, items or FlashImmunityComponent.
/// Raised on the target hit by the flash and their inventory items.
/// </summary>
[ByRefEvent]
public record struct FlashAttemptEvent(EntityUid Target, EntityUid? User, EntityUid? Used, bool Cancelled = false) : IInventoryRelayEvent
{
    public bool IsImmunityQuery; // Starlight

    SlotFlags IInventoryRelayEvent.TargetSlots => SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.MASK;
}

/// <summary>
/// Called when a player is successfully flashed, once for each flashed player.
/// Raised on the target hit by the flash, the user of the flash and the flash used.
/// The Melee parameter is used to check for rev conversion.
/// </summary>
[ByRefEvent]
public record struct AfterFlashedEvent(EntityUid Target, EntityUid? User, EntityUid? Used, bool Melee);

/// <summary>
/// Raised once on the flash entity when it was used, regardless of the flashed status being applied or not.
/// </summary>
[ByRefEvent]
public record struct AfterFlashActivatedEvent(EntityUid? Target, EntityUid? User);

// Moffstation - Start
/// <summary>
/// This event is raised on an entity when its flash immunity is changed (or at least our best effort at that, it might
/// be raised when there's no change). This is usually the result of the entity un/equipping flash-immune gear, but this
/// is also raised on the gear itself (or a mob if it has the <see cref="FlashImmunityComponent"/> itself).
/// </summary>
[ByRefEvent]
public struct FlashImmunityChangedEvent(bool flashImmune)
{
    public readonly bool FlashImmune = flashImmune;
}
// Moffstation - End
