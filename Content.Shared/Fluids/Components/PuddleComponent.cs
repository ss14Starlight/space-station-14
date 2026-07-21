using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Fluids.Components
{
    /// <summary>
    /// Puddle on a floor
    /// </summary>
    [RegisterComponent, NetworkedComponent, Access(typeof(SharedPuddleSystem))]
    public sealed partial class PuddleComponent : Component
    {
        [DataField]
        public SoundSpecifier SpillSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

        [DataField]
        public FixedPoint2 OverflowVolume = FixedPoint2.New(50);

        [DataField("solution")] public string SolutionName = "puddle";

        /// <summary>
        /// Default minimum speed someone must be moving to slip for all reagents.
        /// </summary>
        [DataField]
        public float DefaultSlippery = 5.5f;

        [ViewVariables]
        public Entity<SolutionComponent>? Solution;

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
