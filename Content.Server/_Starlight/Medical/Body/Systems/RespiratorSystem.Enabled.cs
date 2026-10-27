using Content.Server.Body.Components;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class RespiratorSystem
{
    /// <summary>
    /// Suspend or resume respiration without losing the body's breathing configuration.
    /// </summary>
    /// <param name="ent">The entity whose respirator is toggled; the component is not added if absent.</param>
    /// <param name="enabled">Whether respiratory updates and gas exchange should run.</param>
    /// <returns>False if the respirator cannot be resolved; true even if it already has the requested state.</returns>
    /// <remarks>
    /// A state change schedules the next tick one adjusted interval from now. Disabling also resets
    /// consecutive suffocation cycles and raises <see cref="StopSuffocatingEvent"/> to clear warnings.
    /// Neither transition heals damage, refills saturation, empties lungs, or resets the breath phase.
    /// Repeating the current state has no side effects.
    /// </remarks>
    public bool SetEnabled(Entity<RespiratorComponent?> ent, bool enabled)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false))
            return false;

        if (ent.Comp.Enabled == enabled)
            return true;

        ent.Comp.Enabled = enabled;
        ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.AdjustedUpdateInterval;
        if (!enabled)
        {
            ent.Comp.SuffocationCycles = 0;
            var ev = new StopSuffocatingEvent();
            RaiseLocalEvent(ent, ref ev);
        }

        return true;
    }

}
