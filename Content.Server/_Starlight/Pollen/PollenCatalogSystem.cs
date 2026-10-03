using Content.Shared.Botany.Items.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class PollenCatalogSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private Dictionary<string, EntProtoId>? _plants;

    private static readonly Dictionary<string, string> _customPollenNames = new()
    {
        ["advancedpollen"] = "pollen-name-advancedpollen",
    };

    public IEnumerable<string> PollenIds => GetPlants().Keys;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _plants = null);
    }

    public bool TryGetProduce(string pollenId, out EntProtoId produce) =>
        GetPlants().TryGetValue(pollenId, out produce);

    public string GetName(string pollenId)
    {
        if (_customPollenNames.TryGetValue(pollenId, out var locKey))
            return Loc.GetString(locKey);

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

            result.TryAdd(plantId.ToString(), proto.ID);
        }

        return result;
    }
}
