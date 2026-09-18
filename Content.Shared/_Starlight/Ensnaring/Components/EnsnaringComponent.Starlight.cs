// ReSharper disable CheckNamespace
using Robust.Shared.Prototypes;

namespace Content.Shared.Ensnaring.Components;

public sealed partial class EnsnaringComponent
{
    /// <summary>
    /// Should this ensnare someone when impact is made?
    /// </summary>
    [DataField]
    public bool CanImpactTrigger;

    /// <summary>
    /// Has the ensnaring been handled?
    /// </summary>
    [DataField]
    public bool EnsnaredHandled = false;

    /// <summary>
    /// If we want to replace the original ensnaring item with something else when freed.
    /// </summary>
    [DataField]
    public EntProtoId? ensnareFreedPrototype;
}
