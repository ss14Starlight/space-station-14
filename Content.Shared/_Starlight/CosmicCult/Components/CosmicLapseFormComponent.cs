using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CosmicCult.Components;

/// <summary>
/// Overrides which lapse form an entity turns into when lapsed.
/// </summary>
[RegisterComponent]
public sealed partial class CosmicLapseFormComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Form = default!;
}
