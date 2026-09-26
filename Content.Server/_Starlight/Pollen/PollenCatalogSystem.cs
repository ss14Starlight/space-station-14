using Robust.Shared.Prototypes;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;

namespace Content.Server._Starlight.Pollen.Systems;

/// <summary>
/// Maps pollen ids (= produce SeedId, e.g. "wheat") to the produce prototype
/// that emits them (e.g. WheatBushel). One entry per plant type, not per entity.
/// </summary>
public sealed partial class PollenCatalogSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private Dictionary<string, EntProtoId>? _plants;

    public IEnumerable<string> PollenIds => GetPlants().Keys;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _plants = null);
    }

    public bool TryGetProduce(string pollenId, out EntProtoId produce) =>
        GetPlants().TryGetValue(pollenId, out produce);

    /// <summary>The `name:` of the produce prototype, e.g. "wheat bushel".</summary>
    public string GetName(string pollenId)
    {
        if (GetPlants().TryGetValue(pollenId, out var produce) &&
            _prototype.TryIndex(produce, out var proto))
            return proto.Name;

        return pollenId;
    }

    private Dictionary<string, EntProtoId> GetPlants() => _plants ??= Build();

    private Dictionary<string, EntProtoId> Build()
    {
        var result = new Dictionary<string, EntProtoId>();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.Components.ContainsKey("EmitPollen"))
                continue;

            if (!proto.Components.TryGetValue("Produce", out var entry) ||
                entry.Component is not ProduceComponent produce ||
                produce.PlantProtoId is not { } plantId)
                continue;

            result.TryAdd(plantId.ToString(), proto.ID); // first prototype per seed wins
        }

        return result;
    }
}
