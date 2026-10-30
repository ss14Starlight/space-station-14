using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Pollen.Components;

/// <summary>
/// Purchased from the pollen shop. While present, PollenShopSystem injects
/// Amount of Reagent into the bloodstream every Interval. Which reagent,
/// amount, and interval are set at purchase time by PollenShopSystem,
/// based on which listing was bought - see _periodicReagentPerks.
/// Server-only, not networked.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenSerumPerkComponent : Component
{
    /// <summary>
    /// The reagent injected into the bloodstream.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Phytovitalin";

    /// <summary>
    /// The amount of reagent injected per tick.
    /// </summary>
    [DataField]
    public float Amount = 1f;

    /// <summary>
    /// The time between reagent injections.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The time when the next reagent injection is scheduled.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextTick;
}
