using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Plumbing.Components;

/// <summary>
///     A plumbing filter that separates reagents into two outputs.
///     Pulls reagents from the inlet into a buffer, then restricts which reagents
///     can be pulled from each outlet via <see cref="PlumbingPullAttemptEvent"/>.
///     Filter outlet only provides reagents in the filter list.
///     Passthrough outlet only provides reagents NOT in the filter list.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PlumbingFilterComponent : Component
{
    /// <summary>
    ///     Name of the filter outlet node - only filtered reagents can be pulled here.
    /// </summary>
    [DataField]
    public string FilterNodeName = "outletFilter";

    /// <summary>
    ///     Name of the passthrough outlet node - only non-filtered reagents can be pulled here.
    /// </summary>
    [DataField]
    public string PassthroughNodeName = "outletPassthrough";

    /// <summary>
    ///     Name of the solution buffer that holds the reagents.
    /// </summary>
    [DataField]
    public string BufferSolutionName = "buffer";

    /// <summary>
    ///     Whether the filter is currently enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     The reagent IDs to filter out to the filter port.
    ///     Multiple reagents can be filtered simultaneously.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<ReagentPrototype>> FilteredReagents = new();
}
