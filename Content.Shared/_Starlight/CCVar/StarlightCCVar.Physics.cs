using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    // Taken from https://github.com/RMC-14/RMC-14
    public static readonly CVarDef<bool> PhysicsActiveInputMoverEnabled =
        CVarDef.Create("physics.active_input_mover_enabled", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// solve mover velocities once per tick instead of every substep. input doesn't change mid-tick and
    /// only the last substep gets networked anyway, so the rest just coast. roughly halves the mover pass
    /// at substeps=2. left as a toggle in case the accel/friction feel ends up off. replicated so the
    /// client gates the same way, otherwise it mispredicts and you rubber-band.
    /// </summary>
    public static readonly CVarDef<bool> PhysicsMoverSubstepGating =
        CVarDef.Create("physics.mover_substep_gating", false, CVar.REPLICATED | CVar.SERVER);
}
