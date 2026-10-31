using System.Linq;
using Content.Server.Worldgen.Tools;
using Content.Shared.Maps;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GasMining.GasAsteroid;

[RegisterComponent]
[Access(typeof(GasAsteroidFloorPlanPopulatorSystem))]
public sealed partial class GasAsteroidFloorPlanPopulatorComponent : Component
{
    private Dictionary<string, EntitySpawnCollectionCache>? _caches;

    [DataField("entries", required: true)]
    private Dictionary<ProtoId<ContentTileDefinition>, List<EntitySpawnEntry>> _entries = default!;

    [ViewVariables]
    public Dictionary<string, EntitySpawnCollectionCache> Caches
    {
        get
        {
            _caches ??= _entries
                    .Select(x =>
                        new KeyValuePair<string, EntitySpawnCollectionCache>(x.Key,
                            new EntitySpawnCollectionCache(x.Value)))
                    .ToDictionary(x => x.Key, x => x.Value);

            return _caches;
        }
    }

    // Minimum number of gas nodes
    [DataField]
    public int MinNodes = 1;
    // Maximum number of gas nodes
    [DataField]
    public int MaxNodes = 3;
}
