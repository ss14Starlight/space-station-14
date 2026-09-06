using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids.Components
{
    /// <summary>
    /// Puddle on a floor
    /// </summary>
    public sealed partial class PuddleComponent : Component
    {
        // Funky start - Footprints
        /// <summary>
        /// Whether or not this puddle applies the effects of its contents' <see cref="ReagentPrototype.Viscosity"/> and
        /// <see cref="ReagentPrototype.Friction"/>.
        /// </summary>
        [DataField]
        public bool AffectsMovement = true;

        /// <summary>
        /// Whether or not this puddle applies the effects of its contents' <see cref="ReagentPrototype.FootstepSound"/>.
        /// </summary>
        [DataField]
        public bool AffectsSound = true;
        // Funky end

        // Moff start - footprints
        /// <summary>
        /// Whether or not this puddle can apply stains.
        /// </summary>
        [DataField]
        public bool CausesStains = true;
        // Moff end
    }
}
