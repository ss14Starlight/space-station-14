using Content.Server._Starlight.Atmos.AirlockCycler;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Piping.Unary.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasVentPumpSystem
{
    /// <summary>
    /// Allows cycler to set vent pump state like enabled, target pressure, pump dir.
    /// The vent's own settings are saved the first time, see <see cref="ReleaseAirlockCyclerControl"/>.
    /// </summary>
    public void SetAirlockCyclerMode(Entity<GasVentPumpComponent?> vent, bool enabled, VentPumpDirection direction, float targetPressure)
    {
        if (!Resolve(vent, ref vent.Comp))
            return;

        var comp = vent.Comp;
        if (!HasComp<AirlockCyclerVentComponent>(vent))
        {
            var saved = AddComp<AirlockCyclerVentComponent>(vent);
            saved.Enabled = comp.Enabled;
            saved.PumpDirection = comp.PumpDirection;
            saved.PressureChecks = comp.PressureChecks;
            saved.ExternalPressureBound = comp.ExternalPressureBound;
            saved.InternalPressureBound = comp.InternalPressureBound;
            saved.PressureLockoutOverride = comp.PressureLockoutOverride;
        }

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

    /// <summary>
    /// Stops the vent if a cycler drives it. Keeps the cycler in control, unlike
    /// <see cref="ReleaseAirlockCyclerControl"/>.
    /// </summary>
    public void StopAirlockCyclerVent(Entity<GasVentPumpComponent?> vent)
    {
        if (!Resolve(vent, ref vent.Comp) || !HasComp<AirlockCyclerVentComponent>(vent))
            return;

        SetAirlockCyclerMode(vent, false, vent.Comp.PumpDirection, vent.Comp.ExternalPressureBound);
    }

    /// <summary>
    /// The cycler lets go of the vent: put back the settings it had before, including its pressure lockout, so the
    /// vent does not keep pumping to the last cycle's bound.
    /// </summary>
    public void ReleaseAirlockCyclerControl(Entity<GasVentPumpComponent?> vent)
    {
        if (!Resolve(vent, ref vent.Comp, false) || !TryComp<AirlockCyclerVentComponent>(vent, out var saved))
            return;

        var comp = vent.Comp;
        comp.Enabled = saved.Enabled;
        comp.PumpDirection = saved.PumpDirection;
        comp.PressureChecks = saved.PressureChecks;
        comp.ExternalPressureBound = saved.ExternalPressureBound;
        comp.InternalPressureBound = saved.InternalPressureBound;
        comp.PressureLockoutOverride = saved.PressureLockoutOverride;
        RemComp<AirlockCyclerVentComponent>(vent);
        UpdateState(vent, comp);
    }
}
