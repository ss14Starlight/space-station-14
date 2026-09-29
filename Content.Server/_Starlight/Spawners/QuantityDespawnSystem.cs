using Content.Shared._Starlight.Spawners.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Spawners;

public sealed partial class QuantityDespawnSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    private readonly Dictionary<EntProtoId<QuantityDespawnCategoryComponent>, Queue<EntityUid>> _ents = new();
    private readonly Dictionary<EntProtoId<QuantityDespawnCategoryComponent>, long> _maxEnts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<QuantityDespawnComponent, MapInitEvent>(OnCreation);
    }

    private void OnCreation(Entity<QuantityDespawnComponent> ent, ref MapInitEvent args)
    {
        var category = ent.Comp.Category;

        if (string.IsNullOrEmpty(category.Id))
            return;

        // first time seeing this type, setup on dict
        if (!_ents.ContainsKey(category))
        {
            _ents.Add(category, new());

            var proto = _prototype.Index(category);
            proto.TryGetComponent<QuantityDespawnCategoryComponent>(out var catComp, _componentFactory);
            _maxEnts.Add(category, catComp?.MaxEntities ?? 1000);
        }

        _ents[category].Enqueue(ent.Owner);
        while (_ents[category].Count > _maxEnts[category])
        {
            // max exceeded, clear old ents
            var uid = _ents[category].Dequeue();
            QueueDel(uid);
        }
    }
}
