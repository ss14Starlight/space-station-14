using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Controls which interaction particles are displayed.
    /// </summary>
    public static readonly CVarDef<int> InteractionParticlesMode =
        CVarDef.Create("opt.interaction_particles_mode", (int) InteractionParticleMode.WithoutInHand, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Experimental fix: rate-limits drunk overlay screen-texture copies to ~30Hz instead of every render frame.
    /// </summary>
    public static readonly CVarDef<bool> DrunkRenderFix =
        CVarDef.Create("opt.drunk_render_fix", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}

/// <summary>
/// Client-side interaction particle visibility.
/// </summary>
public enum InteractionParticleMode
{
    /// <summary>
    /// Shows ordinary interaction particles and the local player's in-hand particles.
    /// </summary>
    All = 0,

    /// <summary>
    /// Shows ordinary interaction particles but no in-hand/inventory particles.
    /// </summary>
    WithoutInHand = 1,

    /// <summary>
    /// Hides every interaction particle.
    /// </summary>
    None = 2,
}
