using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Components;

/// <summary>
/// Purchased from the pollen shop. While present, PollenShopSystem injects
/// <see cref="Reagent"/> into the bloodstream once per second. Server-only,
/// not networked.
/// </summary>
[RegisterComponent]
public sealed partial class PollenSerumPerkComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Omnizine";

    [DataField]
    public float AmountPerSecond = 1f;

    public float Accumulator;
}