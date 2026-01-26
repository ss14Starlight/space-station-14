using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.BloodCult.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class SoulStoneComponent : Component
    {
        /// <summary>
        /// The prototype ID of the original entity that was used to create this soulstone.
        /// When the soulstone breaks, it will revert to this entity type.
        /// </summary>
        [DataField]
        public EntProtoId? OriginalEntityPrototype;
    }
}
