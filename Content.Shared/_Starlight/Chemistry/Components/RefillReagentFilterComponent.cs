using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Chemistry.Components;

/// <summary>
/// Rejects solution transfers into this entity if the source solution contains invalid reagents.
/// </summary>
[RegisterComponent]
public sealed partial class RefillReagentFilterComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new();

    [DataField]
    public LocId Popup = "refill-reagent-filter-unsuitable-reagent";
}
