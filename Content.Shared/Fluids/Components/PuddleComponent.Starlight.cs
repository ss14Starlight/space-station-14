
namespace Content.Shared.Fluids.Components
{
    public sealed partial class PuddleComponent : Component
    {
        /// <summary>
        /// Whether entities colliding with this puddle can receive clothing stains.
        /// </summary>
        [DataField]
        public bool CausesStains = true;
    }
}
