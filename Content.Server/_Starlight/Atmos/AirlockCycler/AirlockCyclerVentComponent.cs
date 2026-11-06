using Content.Shared.Atmos.Piping.Unary.Components;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

/// <summary>
/// Added to a vent while an airlock cycler drives it. Holds the vent's own settings from before, so they come back
/// (pressure bounds, lockout) when the cycler lets go: unlinked, or the controller removed.
/// </summary>
[RegisterComponent]
public sealed partial class AirlockCyclerVentComponent : Component
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
}
