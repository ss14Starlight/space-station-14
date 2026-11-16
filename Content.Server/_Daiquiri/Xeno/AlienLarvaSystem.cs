using Content.Server.Mind;
using Content.Shared._Daiquiri.Xeno;
using Content.Shared._Starlight.Medical.Body.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.Xeno;

public sealed partial class AlienLarvaSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlienLarvaComponent, OrganRemovedFromBodyEvent>(OnRemovedFromBody);
    }

    private void OnRemovedFromBody(EntityUid uid, AlienLarvaComponent comp, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OldBody))
            return;

        if (!_protoManager.TryIndex<EntityPrototype>(comp.LarvaPrototype, out var larvaProto))
            return;

        var coords = Transform(uid).Coordinates;
        var larva = SpawnAtPosition(larvaProto.ID, coords);

        if (comp.TransferMind && _mindSystem.TryGetMind(args.OldBody, out var mindId, out var mind))
            _mindSystem.TransferTo(mindId, larva, mind: mind);

        QueueDel(uid);
    }
}
