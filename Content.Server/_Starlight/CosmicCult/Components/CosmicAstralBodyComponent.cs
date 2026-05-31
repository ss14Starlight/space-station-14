using Content.Server._Starlight.CosmicCult.Abilities;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent, Access(typeof(CosmicReturnSystem))]
public sealed partial class CosmicAstralBodyComponent : Component
{
    [DataField]
    public EntityUid OriginalBody;
}
