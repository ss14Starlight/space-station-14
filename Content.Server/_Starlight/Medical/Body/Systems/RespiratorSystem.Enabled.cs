using Content.Server.Body.Components;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class RespiratorSystem
{
    /// <summary>
    /// Suspend or resume respiration without losing the body's breathing configuration.
    /// Disabling respiration clears suffocation warnings, but does not heal existing damage.
    /// </summary>
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
