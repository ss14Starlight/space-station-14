using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// Just contains the reagent whitelist part of it, so you can accept more than just one.
/// </summary>
public sealed partial class RefillableSolutionComponent : Component
{
    /// <summary>
    /// Reagents that are allowed to be transferred into this solution.
    /// Null allows all reagents.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>>? ReagentWhitelist;
}
