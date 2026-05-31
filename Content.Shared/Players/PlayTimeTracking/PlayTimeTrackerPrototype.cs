using Robust.Shared.Prototypes;

namespace Content.Shared.Players.PlayTimeTracking;

/// <summary>
/// Given to a role to specify its ID for role-timer tracking purposes. That's it.
/// </summary>
[Prototype]
public sealed partial class PlayTimeTrackerPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    // Starlight start
    /// <summary>
    ///     The name of this job as displayed to players.
    /// </summary>
    [DataField]
    public LocId Name { get; private set; } = "generic-unknown";

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);
    // Starlight end
}
