using Content.Server.Hands.Systems;
using Content.Shared._Starlight.Devil.DamnationActions;
using Content.Shared.EntityTable;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;

namespace Content.Server._Starlight.Devil.DamnationActions;

public sealed partial class DamnationActionSpawnLoot : DamnationAction
{
    [DataField]
    public ProtoId<EntityTablePrototype> Table = "PowerDamnationRandomArmorTable";

    [DataField]
    public bool TryPickup = true;

    private IPrototypeManager _prototypeManager = default!;
    private EntityTableSystem _entityTable = default!;
    private TransformSystem _transform = default!;
    private HandsSystem _hands = default!;

    public override bool Action(Entity<DamnedComponent> victim)
    {
        if (_prototypeManager.Index(Table) is not EntityTablePrototype tableProto)
            return false;

        var spawns = _entityTable.GetSpawns(tableProto);
        var coordinates = _transform.GetMoverCoordinates(victim);
        foreach (var spawn in spawns)
        {
            var uid = _entityManager.SpawnAtPosition(spawn, coordinates);
            if (TryPickup)
                _hands.TryPickupAnyHand(victim, uid);
        }

        return true;
    }

    public override void ResolveIoC()
    {
        base.ResolveIoC();

        _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _entityTable = _entityManager.System<EntityTableSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _hands = _entityManager.System<HandsSystem>();
    }
}
