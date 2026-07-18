using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Construction.Components;

/// <summary>
/// Tracks in-progress initial construction reservations on a user while a DoAfter runs.
/// </summary>
[RegisterComponent, Access(typeof(ConstructionSystem))]
public sealed partial class PendingInitialConstructionComponent : Component
{
    public uint NextOperationId = 1;

    [ViewVariables]
    public Dictionary<uint, PendingInitialConstruction> Operations = new();
}

public enum InitialConstructionKind : byte
{
    Item,
    Structure,
}

public sealed class PendingInitialConstruction
{
    public InitialConstructionKind Kind;
    public ProtoId<ConstructionPrototype> ConstructionId;
    public string GraphId = string.Empty;
    public string EdgeTarget = string.Empty;
    public string TargetNode = string.Empty;
    public string PrimaryContainerId = string.Empty;
    public Dictionary<string, string> StoreContainers = new();
    public EntityCoordinates Coordinates;
    public Angle Angle;
    public int? StructureAck;
    public NetUserId? SessionUserId;
}
