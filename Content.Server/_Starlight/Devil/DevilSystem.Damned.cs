using Content.Server.Popups;
using Content.Shared._Starlight.Devil;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private PopupSystem _popup = default!;
    private void SubscribeDamned()
    {

    }

    private bool CanDamn(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        return !entity.Comp.Damnations.Contains(proto);
    }

    private bool AddDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        // here we shove all the components in, and then await their potential fails later via the event
        if (!CanDamn(entity, proto)) return false;
        if (!_prototype.TryIndex(proto, out var damnationPrototype)) return false;

        _entityManager.AddComponents(entity.Owner, damnationPrototype.Components);
        _entityManager.RemoveComponents(entity.Owner, damnationPrototype.RemovedComponents);
        entity.Comp.NetCost += damnationPrototype.Cost;
        entity.Comp.Damnations.Add(proto);

        return true;
    }

    private bool DamnEntity(EntityUid ent, InfernalContractData contract, EntityUid devil)
    {
        EnsureComp<DamnedComponent>(ent, out var damnedComp);
        if (damnedComp == null) return false;

        damnedComp.DamnedBy = devil;

        // check to see that all of the damnations will work, before we try to add any
        foreach (var damnation in contract.Damnations)
            if (!CanDamn((ent, damnedComp), damnation)) return false;

        foreach (var damnation in contract.Damnations)
        {
            AddDamnation((ent, damnedComp), damnation);
        }

        _popup.PopupEntity(Loc.GetString("devil-popup-damnation", ("name", Name(ent))), ent, Shared.Popups.PopupType.MediumCaution);

        return true;
    }

    private bool RemoveDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> damnation)
    {
        if (!entity.Comp.Damnations.Contains(damnation)) return false;
        if (!_prototype.TryIndex(damnation, out var damnationPrototype)) return false;

        if (damnationPrototype.ReverseOnRemove)
            _entityManager.RemoveComponents(entity.Owner, damnationPrototype.Components);

        entity.Comp.Damnations.Remove(damnation);

        return true;
    }
}