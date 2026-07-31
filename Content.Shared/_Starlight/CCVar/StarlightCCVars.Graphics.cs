using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether hand, item, and pulling interaction particles are displayed.
    /// </summary>
    public static readonly CVarDef<bool> InteractionParticlesEnabled =
        CVarDef.Create("opt.interaction_particles_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
