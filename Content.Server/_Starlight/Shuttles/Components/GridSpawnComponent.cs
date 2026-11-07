using Content.Server.Shuttles.Components;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Shuttles.Components;

[DataRecord]
public sealed partial class EntitySpawnGroup : IGridSpawnGroup
{
    public List<ProtoId<EntityPrototype>> Protos = new();

    /// <inheritdoc />
    public float MinimumDistance { get; }

    /// <inheritdoc />
    public float MaximumDistance { get; }
    public ProtoId<LocalizedDatasetPrototype>? NameDataset { get; }
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public ComponentRegistry AddComponents { get; set; } = new();
    public bool Hide { get; set; } = false;
    public bool NameGrid { get; set; } = true;
    public bool StationGrid { get; set; } = true;
}
