using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
/// Tracks active vampire drain beam connections for Blood Bringers Rite
/// </summary>
[RegisterComponent]
public sealed partial class VampireDrainBeamComponent : Component
{
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

    public VampireDrainBeamEvent(NetEntity source, NetEntity target, bool create)
    {
        Source = source;
        Target = target;
        Create = create;
    }
}
