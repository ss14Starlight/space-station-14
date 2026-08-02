using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Plumbing.Components;

/// <summary>
///     A plumbing filter that extracts specific reagents from a duct network.
///     Only the reagents in the filter list are pulled from the inlet into the buffer,
///     the same demand-driven way the plumbing reactor pulls its targets, except that
///     every filtered reagent has the same implicit target of <see cref="ReagentCapacity"/>.
///     Anything else is left on the network. The buffer is served to the outlet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PlumbingFilterComponent : Component
{
    public const int MaxFilteredReagents = 10;

    /// <summary>
    ///     How much of each filtered reagent the buffer holds. Each reagent gets its own
    ///     allowance, so a plentiful reagent cannot crowd the rest out of the buffer.
    ///     The buffer solution should be sized for
    ///     <see cref="MaxFilteredReagents"/> * this value.
    /// </summary>
    [DataField]
    public FixedPoint2 ReagentCapacity = FixedPoint2.New(10);

    /// <summary>
    ///     Whether the filter is currently enabled. A disabled filter stops pulling,
    ///     but whatever is already buffered can still be drawn from the outlet.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     The reagent IDs to pull in from the network.
    ///     Multiple reagents can be filtered simultaneously.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<ReagentPrototype>> FilteredReagents = new();

    /// <summary>
    ///     Rotates which filtered reagent gets first claim on the transfer budget each update,
    ///     so a plentiful reagent early in the list can't starve the rest.
    /// </summary>
    public int ReagentRoundRobinIndex;
}
