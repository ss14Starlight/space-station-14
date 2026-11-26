using Content.Server._Starlight.Atmos.AirlockCycler;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Piping.Unary.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasVentScrubberSystem
{
    /// <summary>
    /// Lets an airlock cycler siphon the chamber through this scrubber, or stop it. The scrubber's own settings are
    /// saved the first time, see <see cref="ReleaseAirlockCyclerControl"/>.
    /// </summary>
    public void SetAirlockCyclerMode(Entity<GasVentScrubberComponent?> scrubber, bool siphon)
    {
        if (!Resolve(scrubber, ref scrubber.Comp, false))
            return;

        var comp = scrubber.Comp;
        if (!HasComp<AirlockCyclerDeviceComponent>(scrubber))
        {
            var saved = AddComp<AirlockCyclerDeviceComponent>(scrubber);
            saved.Enabled = comp.Enabled;
            saved.ScrubberDirection = comp.PumpDirection;
        }

        if (comp.Enabled == siphon && (!siphon || comp.PumpDirection == ScrubberPumpDirection.Siphoning))
            return;

        comp.Enabled = siphon;
        if (siphon)
            comp.PumpDirection = ScrubberPumpDirection.Siphoning;

        UpdateState(scrubber, comp);
    }

    /// <summary>
    /// Stops the scrubber and takes it under the cycler, saving its own settings.
    /// </summary>
    public void StopAirlockCyclerScrubber(Entity<GasVentScrubberComponent?> scrubber)
        => SetAirlockCyclerMode(scrubber, false);

    /// <summary>
    /// The cycler lets go of the scrubber: put back its own mode and on/off state.
    /// </summary>
    public void ReleaseAirlockCyclerControl(Entity<GasVentScrubberComponent?> scrubber)
    {
        if (!Resolve(scrubber, ref scrubber.Comp, false) || !TryComp<AirlockCyclerDeviceComponent>(scrubber, out var saved))
            return;

        scrubber.Comp.Enabled = saved.Enabled;
        scrubber.Comp.PumpDirection = saved.ScrubberDirection;
        RemComp<AirlockCyclerDeviceComponent>(scrubber);
        UpdateState(scrubber, scrubber.Comp);
    }
}
