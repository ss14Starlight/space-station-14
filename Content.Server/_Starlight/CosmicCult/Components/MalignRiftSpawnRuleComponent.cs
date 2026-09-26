using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.CosmicCult.Components;

[RegisterComponent, Access(typeof(MalignRiftSpawnRule))]
public sealed partial class MalignRiftSpawnRuleComponent : Component
{
    [DataField] public EntProtoId MalignRift = "CosmicMalignRift";
    [DataField] public SoundSpecifier Tier2Sound = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/tier2.ogg");
}
