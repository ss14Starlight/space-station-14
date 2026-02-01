using Content.Server.Shuttles.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Components;

/// <summary>
/// If added to an airlock will try to autofill a grid onto it on MapInit
/// using a randomly-selected grid from a list.
/// </summary>
[RegisterComponent, Access(typeof(ShuttleSystem))]
public sealed partial class RandomGridFillComponent : Component
{
    /// <summary>
    /// path to weight mapping
    /// </summary>
    [DataField("pathWeights", required: true)]
    public Dictionary<ResPath, float> PathWeights = new();

    /// <summary>
    /// Components to be added to any spawned grids.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();
}
