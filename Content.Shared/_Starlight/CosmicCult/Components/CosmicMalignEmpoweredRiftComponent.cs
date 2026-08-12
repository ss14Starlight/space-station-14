using Robust.Shared.Containers;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent] public sealed partial class CosmicMalignEmpoweredRiftComponent : Component
{
    public const string CorpseContainerId = "cosmic-empowered-rift-corpse";
    public Container CorpseContainer = default!;

    /// <summary>
    /// Controls how quickly corpses stored inside the rift are cooled.
    /// Higher values increase the rate of heat removal.
    /// </summary>
    [DataField] public float CoolingCoefficient = 0.3f;
}
