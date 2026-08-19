using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent, NetworkedComponent] public sealed partial class CosmicMalignEmpoweredRiftComponent : Component
{
    public const string CorpseContainerId = "cosmic-empowered-rift-corpse";
    public Container CorpseContainer = default!;

    /// <summary>
    /// Controls how quickly corpses stored inside the rift are cooled.
    /// Higher values increase the rate of heat removal.
    /// </summary>
    [DataField] public float CoolingCoefficient = 0.3f;
    
    public bool IsOccupied;
    [Serializable, NetSerializable]
    public sealed class State : ComponentState
    {
        public bool IsOccupied;
    }
}
