using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Components;

/// <summary>
/// Purchased from the pollen shop. While present, PollenShopSystem injects
/// Amount of Reagent into the bloodstream every Interval. Which reagent,
/// amount, and interval are set at purchase time by PollenShopSystem,
/// based on which listing was bought - see _periodicReagentPerks.
/// Server-only, not networked.
/// </summary>
[RegisterComponent]
public sealed partial class PollenSerumPerkComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "RobustHarvest";

    [DataField]
    public float Amount = 1f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    public TimeSpan NextTick;
}