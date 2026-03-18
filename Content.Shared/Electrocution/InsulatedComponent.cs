using Robust.Shared.GameStates;

#region Starlight
using Content.Shared.Genetics;
#endregion Starlight

namespace Content.Shared.Electrocution
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    [Access(typeof(SharedElectrocutionSystem), typeof(SharedGeneticsSystem))] // Starlight-edit - add GeneticsSystem access
    [GeneticComponent(5,2)] // Starlight - add genetics for biological insulation
    public sealed partial class InsulatedComponent : Component
    {
        // Technically, people could cheat and figure out which budget insulated gloves are gud and which ones are bad.
        // We might want to rethink this a little bit.
        /// <summary>
        ///     Siemens coefficient. Zero means completely insulated.
        /// </summary>
        [DataField, AutoNetworkedField]
        [GeneticMultiValueVariable<float>(0f, 4f, 2f, 1.5f, 0.5f, 0f)] // Starlight
        public float Coefficient { get; set; } = 0f;
    }
}
