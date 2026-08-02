using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Classifies what a piece of pathogen PPE physically is. Protection values are evaluated
/// from the wearer's complete outfit by <see cref="PathogenResistanceSystem"/> and never
/// authored on individual clothing prototypes.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class PathogenResistanceComponent : Component
{
    [DataField(required: true)]
    public PathogenProtectionClass Class;
}

public enum PathogenProtectionClass : byte
{
    /// <summary>
    /// A gas or sterile mask containing filter media. It partially filters station air
    /// even without internals.
    /// </summary>
    FilterMask,

    /// <summary>
    /// A breath mask, medical oxygen mask, or pressure helmet. It is an air-supply
    /// interface, not a filter, and offers no pathogen protection unless internals works.
    /// </summary>
    SupplyMask,

    /// <summary>
    /// Sterile single-use gloves. Reused work, combat, and insulated gloves are fomites,
    /// not pathogen PPE.
    /// </summary>
    SterileBarrier,

    /// <summary>
    /// Purpose-built clean bio suit.
    /// </summary>
    BioSuit,

    /// <summary>
    /// Purpose-built filtered bio hood.
    /// </summary>
    BioHood,

    /// <summary>
    /// Hardsuit or EVA outer suit. It blocks settling spores well, but is not sterile.
    /// </summary>
    SealedSuit,
}

/// <summary>
/// Collects the physical PPE classes present in the requested worn slots.
/// </summary>
public sealed class PathogenResistanceQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; }

    public readonly HashSet<PathogenProtectionClass> Classes = [];

    public PathogenResistanceQueryEvent(SlotFlags slots)
    {
        TargetSlots = slots;
    }
}
