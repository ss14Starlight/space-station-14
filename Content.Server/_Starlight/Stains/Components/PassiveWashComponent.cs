using Content.Server._Starlight.Stains.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.Server._Starlight.Stains.Components;

/// <summary>
/// Gradually washes selected equipment and footprint residue from entities on this surface.
/// </summary>
[RegisterComponent, Access(typeof(PassiveWashSystem))]
public sealed partial class PassiveWashComponent : Component
{
    /// <summary>
    /// Time between wash updates.
    /// </summary>
    [DataField]
    public float WashInterval = 1f;

    /// <summary>
    /// Solution volume removed per update.
    /// </summary>
    [DataField]
    public FixedPoint2 WashAmount = 0.1f;

    /// <summary>
    /// Equipment slots to wash.
    /// </summary>
    [DataField]
    public SlotFlags WashSlots = SlotFlags.WITHOUT_POCKET;

    /// <summary>
    /// Whether to wash the wearer's carried footprint solution as well.
    /// </summary>
    [DataField]
    public bool WashFootprints;
}
