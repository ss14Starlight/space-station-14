using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
/// Tracks active vampire blood bond beam connections for Dantalion's Blood Bond ability
/// </summary>
[RegisterComponent]
public sealed partial class VampireBloodBondBeamComponent : Component
{
    [DataField(required: true)]
    public EntProtoId VisualPrototype;

    /// <summary>
    /// Active beam connections where this entity is the source
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, BloodBondBeamConnection> ActiveBeams = new();
}

/// <summary>
/// Data for blood bond beam connection
/// </summary>
[DataRecord]
public readonly partial record struct BloodBondBeamConnection(
    EntityUid Source,
    EntityUid Target,
    float MaxRange
);

/// <summary>
/// Network event to create/update blood bond beam on client
/// </summary>
[Serializable, NetSerializable]
public sealed class VampireBloodBondBeamEvent : EntityEventArgs
{
    public NetEntity Source { get; }
    public NetEntity Target { get; }
    public bool Create { get; }
    public string VisualPrototype { get; }

    public VampireBloodBondBeamEvent(NetEntity source, NetEntity target, bool create, string visualPrototype)
    {
        Source = source;
        Target = target;
        Create = create;
        VisualPrototype = visualPrototype;
    }
}
