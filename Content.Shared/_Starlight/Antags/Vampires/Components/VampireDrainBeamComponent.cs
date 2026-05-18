using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
/// Tracks active vampire drain beam connections for Blood Bringers Rite
/// </summary>
[RegisterComponent]
public sealed partial class VampireDrainBeamComponent : Component
{
    [DataField(required: true)]
    public EntProtoId VisualPrototype;

    /// <summary>
    /// Active beam connections where this entity is the source
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, DrainBeamConnection> ActiveBeams = new();
}

/// <summary>
/// Data for drain beam connection
/// </summary>
[DataRecord]
public readonly partial record struct DrainBeamConnection(
    EntityUid Source,
    EntityUid Target,
    float MaxRange
);

/// <summary>
/// Network event to create/update drain beam on client
/// </summary>
[Serializable, NetSerializable]
public sealed class VampireDrainBeamEvent : EntityEventArgs
{
    public NetEntity Source { get; }
    public NetEntity Target { get; }
    public bool Create { get; }
    public string VisualPrototype { get; }

    public VampireDrainBeamEvent(NetEntity source, NetEntity target, bool create, string visualPrototype)
    {
        Source = source;
        Target = target;
        Create = create;
        VisualPrototype = visualPrototype;
    }
}
