using Content.Shared._Starlight.Light.NightLighting;

namespace Content.Server._Starlight.Light.NightShift;

/// <summary>
/// Station-wide night shift state, added to stations by <see cref="NightShiftSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(NightShiftSystem))]
public sealed partial class StationNightShiftComponent : Component
{
    /// <summary>
    /// Admin override. <see cref="ApcLightingMode.Auto"/> follows the schedule and events.
    /// </summary>
    [DataField]
    public ApcLightingMode Override = ApcLightingMode.Auto;

    /// <summary>
    /// Alert levels the night shift keeps running at. Anything else turns the lights back up.
    /// </summary>
    [DataField]
    public HashSet<string> PermittedAlertLevels = new() { "green", "blue" };

    /// <summary>
    /// Whether the station is currently in its night shift.
    /// </summary>
    [ViewVariables]
    public bool Night;

    /// <summary>
    /// Whether the alert level is keeping the night shift off.
    /// </summary>
    [ViewVariables]
    public bool Suppressed;

    /// <summary>
    /// Game rules and other sources forcing the night shift outside of its schedule.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> ForcedBy = new();
}
