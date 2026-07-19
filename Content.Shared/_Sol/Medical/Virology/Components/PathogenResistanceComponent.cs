using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Clothing-based pathogen resistance. Coefficient 1 = no protection, 0 = full protection.
/// Multiplies by transmission route.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenResistanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ContactCoefficient = 1f;

    [DataField, AutoNetworkedField]
    public float AirborneCoefficient = 1f;

    [DataField, AutoNetworkedField]
    public float FluidCoefficient = 1f;

    [DataField, AutoNetworkedField]
    public float SurgeryCoefficient = 1f;

    /// <summary>
    /// If true, this item must be paired with other sealed PPE for full effect (hood/suit).
    /// Partial wear applies <see cref="UnsealedPenalty"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiresSeal;

    [DataField, AutoNetworkedField]
    public float UnsealedPenalty = 0.5f;
}

/// <summary>
/// Inventory relay query for total pathogen resistance by route.
/// </summary>
public sealed class PathogenResistanceQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; }

    public PathogenTransmission Transmission { get; }

    public float TotalCoefficient = 1f;

    public bool HasSealedSuit;

    public bool HasSealedHood;

    public PathogenResistanceQueryEvent(SlotFlags slots, PathogenTransmission transmission)
    {
        TargetSlots = slots;
        Transmission = transmission;
    }
}
