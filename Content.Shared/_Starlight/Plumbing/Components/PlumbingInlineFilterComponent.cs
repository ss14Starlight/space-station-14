using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Plumbing.Components;

/// <summary>
///     An inline plumbing filter that extracts specific reagents from a duct network.
///     Only the reagents in the filter list are pulled from the inlet into the buffer,
///     same as the plumbing reactor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PlumbingInlineFilterComponent : Component
{
    public const int MaxFilteredReagents = 10;

    /// <summary>
    ///     How much of each filtered reagent the buffer holds. Each reagent gets its own
    ///     allowance to avoid clogging so a plentiful reagent cannot crowd the rest out of the buffer.
    ///     Should be sized for <see cref="MaxFilteredReagents"/> * this value.
    /// </summary>
    [DataField] public FixedPoint2 ReagentCapacity = FixedPoint2.New(10);

    [DataField, AutoNetworkedField] public bool Enabled = true;

    /// <summary>
    ///     The reagent IDs to pull in from the pipenet.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<ProtoId<ReagentPrototype>> FilteredReagents = new();

    public int ReagentRoundRobinIndex;
}
