using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Piping.Unary.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasVentPumpSystem
{
    /// <summary>
    /// Allows cycler to set vent pump state like enabled, target pressure, pump dir.
    /// </summary>
    public void SetAirlockCyclerMode(Entity<GasVentPumpComponent> vent, bool enabled, VentPumpDirection direction, float targetPressure)
    {
        var comp = vent.Comp;
        if (comp.Enabled == enabled
            && comp.PumpDirection == direction
            && comp.PressureChecks == VentPressureBound.ExternalBound
            && MathF.Abs(comp.ExternalPressureBound - targetPressure) < 0.01f
            && comp.PressureLockoutOverride)
            return;

        comp.Enabled = enabled;
        comp.PumpDirection = direction;
        comp.PressureChecks = VentPressureBound.ExternalBound;
        comp.ExternalPressureBound = targetPressure;
        comp.PressureLockoutOverride = true;
        UpdateState(vent, comp);
    }
}
