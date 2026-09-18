using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent
{
    public sealed partial class ReagentPrototype : IPrototype, IInheritingPrototype
    {
        /// <summary>
        /// Minimum quantity required for this reagent's viscosity to apply in a puddle.
        /// </summary>
        [DataField]
        public FixedPoint2 ViscosityMin = FixedPoint2.Zero;
    }
}
