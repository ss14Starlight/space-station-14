using Content.Server._Starlight.StationEvents.Events;
using Content.Shared.Whitelist;

namespace Content.Server._Starlight.StationEvents.Components;

/// <summary>
///     Cuts a number of wires on whitelisted devices across the station.
/// </summary>
[RegisterComponent, Access(typeof(WireCutRule))]
public sealed partial class WireCutRuleComponent : Component
{
    /// <summary>
    ///     Entities that may have their wires cut. If null, any entity with wires is eligible.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    ///     Entities that should never have their wires cut.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    ///     Minimum number of wires to cut.
    /// </summary>
    [DataField]
    public int MinWires = 1;

    /// <summary>
    ///     Maximum number of wires to cut.
    /// </summary>
    [DataField]
    public int MaxWires = 4;

    /// <summary>
    ///     Maximum number of wires to cut on a single entity.
    /// </summary>
    [DataField]
    public int MaxWiresPerEntity = 1;
}
