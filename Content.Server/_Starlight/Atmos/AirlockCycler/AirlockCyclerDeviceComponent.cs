using Content.Shared.Atmos.Piping.Unary.Components;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

/// <summary>
/// Added to a vent, scrubber or pump while an airlock cycler drives it. Holds the device's own settings from before,
/// so they come back (pressure bounds, lockout, scrub mode, on/off) when the cycler lets go: unlinked, or the
/// controller removed. Only the fields of the device's own kind are used.
/// </summary>
[RegisterComponent]
public sealed partial class AirlockCyclerDeviceComponent : Component
{
    [ViewVariables, DataField]
    public bool Enabled;

    [ViewVariables, DataField]
    public VentPumpDirection PumpDirection;

    [ViewVariables, DataField]
    public VentPressureBound PressureChecks;

    [ViewVariables, DataField]
    public float ExternalPressureBound;

    [ViewVariables, DataField]
    public float InternalPressureBound;

    [ViewVariables, DataField]
    public bool PressureLockoutOverride;

    [ViewVariables, DataField]
    public ScrubberPumpDirection ScrubberDirection;
}
