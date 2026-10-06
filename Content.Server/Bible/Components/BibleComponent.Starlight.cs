using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Bible.Components
{
    public sealed partial class BibleComponent : Component
    {
        /// <summary>
        /// what is the chance a successfull bible thwack removes the cluwning.
        /// </summary>
        [DataField]
        public float CluwneCureChance = 0.03f;

        /// <summary>
        /// if a item has this tag. the unremovable comp is ignored when dropping the item.
        /// </summary>
        [DataField]
        public ProtoId<TagPrototype> RemovableAnywaysTag = "BibleThwackRemovable";
    }
}
