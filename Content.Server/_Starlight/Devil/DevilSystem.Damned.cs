using Content.Server.Jobs;
using Content.Shared._Starlight.Devil;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private EntityManager _entityManager = default!;
    private void SubscribeDamned()
    {

    }

    private bool AddDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        // here we shove all the components in, and then await their potential fails later via the event
        if (entity.Comp.Damnations.Contains(proto)) return false;
        if (!_prototype.TryIndex(proto, out var damnationPrototype)) return false;

        _entityManager.AddComponents(entity.Owner, damnationPrototype.Components);
        _entityManager.RemoveComponents(entity.Owner, damnationPrototype.RemovedComponents);
        entity.Comp.NetCost += damnationPrototype.Cost;
        entity.Comp.Damnations.Add(proto);

        return true;
    }
}