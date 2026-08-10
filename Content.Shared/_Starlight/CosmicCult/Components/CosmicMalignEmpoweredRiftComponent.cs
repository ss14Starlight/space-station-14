using Robust.Shared.Containers;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent] public sealed partial class CosmicMalignEmpoweredRiftComponent : Component
{
    public const string CorpseContainerId = "cosmic-empowered-rift-corpse";

    public Container CorpseContainer = default!;

    [DataField] public float CoolingCoefficient = 0.1f;
}
