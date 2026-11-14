using Content.Shared._Starlight.Zones;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Components;

[RegisterComponent]
public sealed partial class FirestakeConditionComponent : Component
{
    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public EntityUid? AssignedMind;

    [DataField]
    public EntityUid? Grid;

    [DataField]
    public ProtoId<ZonePrototype>? Zone;

    /// <summary>
    /// Minds, rather than bodies, so rebuckling or cloning one victim cannot add more progress.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Victims = [];
}
