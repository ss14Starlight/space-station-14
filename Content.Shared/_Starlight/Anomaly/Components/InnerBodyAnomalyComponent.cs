// ReSharper disable CheckNamespace

using Content.Shared.Anomaly.Effects;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Anomaly.Components;

/// <summary>
/// An anomaly within the body of a living being. Controls the ability to return to the standard state.
/// </summary>
public sealed partial class InnerBodyAnomalyComponent : Component
{
    /// <summary>
    /// Tracks whether the Cosmic Cult faction has already been added to the anomaly host.'
    /// Only if an T3 Culists gets this anom, without it, the T3 cultist would lose the faction.
    /// </summary>
    [DataField]
    public bool AddedCosmicCultFaction;
}
