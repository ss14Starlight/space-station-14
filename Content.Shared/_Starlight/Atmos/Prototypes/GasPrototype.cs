using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.Prototypes
{
    public sealed partial class GasPrototype : IPrototype
    {
        /**
         * Whether this gas is considered to be a reaction moderator.
         * Used to decide if a mixture can be allowed to ignite.
         */
        [DataField]
        public bool IsModerator;
    }
}
