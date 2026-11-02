// ReSharper disable CheckNamespace
using Content.Shared.DisplacementMap;

namespace Content.Shared.Inventory;

public sealed partial class SlotDefinition
{
    /// <summary>
    /// Default clothing displacement for this slot when the wearer has no override.
    /// </summary>
    [DataField] public DisplacementData? Displacement { get; private set; }
}
