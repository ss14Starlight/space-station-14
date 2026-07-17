using Content.Shared._Starlight.Spawners.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Spawners;

public sealed partial class QuantityDespawnSystem : EntitySystem
{
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

        // first time seeing this type, setup on dict
        if (!_ents.ContainsKey(category))
        {
            _ents.Add(category, new());

            // now figure out max for category
            var catEnt = Spawn(category);
            var catComp = Comp<QuantityDespawnCategoryComponent>(catEnt);
            _maxEnts.Add(category, catComp.MaxEntities);
            QueueDel(catEnt);
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
