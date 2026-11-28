using Content.Shared.Inventory;

namespace Content.Shared.Clothing;

public sealed partial class ClothingSpeedModifierSystem
{
    public void InitializeFH()
        => SubscribeLocalEvent<ClothingSpeedModifierComponent, InventoryRelayedEvent<ClothingSpeedModifierQueryEvent>>(OnSlowdownQuery);

    private void OnSlowdownQuery(Entity<ClothingSpeedModifierComponent> ent, ref InventoryRelayedEvent<ClothingSpeedModifierQueryEvent> query)
    {
        if (ent.Comp.RequireActivated || (ent.Comp.Standing is { } standing && !_standing.IsMatchingState(query.Owner, standing))) // Starlight
            return; // Starlight

        query.Args.TotalSprintCoefficient *= ent.Comp.SprintModifier;
        query.Args.TotalWalkCoefficient *= ent.Comp.WalkModifier;
    }
}

public sealed class ClothingSpeedModifierQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    /// All slots to relay to
    /// </summary>
    public SlotFlags TargetSlots { get; }

    /// <summary>
    /// Total clothing speed modifier for Sprinting
    /// </summary>
    public float TotalSprintCoefficient = 1.0f;

    /// <summary>
    /// Total clothing speed modifier for Walking
    /// </summary>
    public float TotalWalkCoefficient = 1.0f;

    public ClothingSpeedModifierQueryEvent(SlotFlags slots)
        => TargetSlots = slots;
}
