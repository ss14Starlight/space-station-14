using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent
{
    public sealed partial class ReagentPrototype : IPrototype, IInheritingPrototype
    {
        /// <summary>
        /// Funky - How flammable this reagent is. Higher values make it catch fire more easily and burn hotter.
        /// </summary>
        [DataField]
        public int Flammability;

        /// <summary>
        /// Funky - If true, this reagent acts as its own oxidizer and can burn in vacuums or oxygen-deprived environments.
        /// </summary>
        [DataField]
        public bool SelfOxidizing;
    }
}
