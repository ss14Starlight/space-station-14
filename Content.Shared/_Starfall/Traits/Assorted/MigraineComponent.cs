using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starfall.Traits.Assorted;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MigraineEffectComponent : Component
{
    public static readonly EntProtoId Prototype = "StatusEffectMigraine";

    /// <summary>
    /// Whether applying this migraine should display popup messages.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShowPopup = true;

    /// <summary>
    /// Popup shown to the person experiencing the migraine. Set to null to disable it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SelfPopup = "trait-chronic-migraines-start";

    /// <summary>
    /// Popup shown to nearby observers. Set to null to disable it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? OthersPopup = "trait-chronic-migraines-others";

    public TimeSpan NextIpcSparkTime;
}

/// <summary>
/// Applied temporarily by preventative migraine medication.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MigrainePreventionEffectComponent : Component
{
    public static readonly EntProtoId Prototype = "StatusEffectMigrainePrevention";

    /// <summary>
    /// Ensures the next chronic migraine cannot occur until at least this much
    /// time has passed after taking preventative medication.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan IncidentDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Tracks the previous end time so status refreshes only add the newly gained
    /// protection time to the migraine timer.
    /// </summary>
    public TimeSpan? LastEndTime;
}

public sealed class MigrainePopupEvent(string? selfPopup, string? othersPopup) : EntityEventArgs
{
    public string? SelfPopup = selfPopup;
    public string? OthersPopup = othersPopup;
    public bool Cancelled;
}
