using Content.Server.Mind;
using Content.Server.Zombies;
using Content.Shared.Species.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared.Body.Systems;

namespace Content.Server.Species.Systems;

public sealed partial class NymphSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private ZombieSystem _zombie = default!;
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NymphComponent, OrganRemovedFromBodyEvent>(OnRemovedFromPart);
    }

    /// <summary>
    /// Converts a removed Diona organ into a nymph. The shared anatomical policy controls only the mind transfer;
    /// ordinary organ removal still creates the nymph even if the former body keeps its mind.
    /// </summary>
    private void OnRemovedFromPart(EntityUid uid, NymphComponent comp, ref OrganRemovedFromBodyEvent args)
    {
        // Deleting native anatomy during regeneration is not an extraction that should create another nymph.
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OldBody)) // Starlight Edit: Target -> OldBody
            return;

        if (!_protoManager.TryIndex<EntityPrototype>(comp.EntityPrototype, out var entityProto))
            return;

        // Get the organs' position & spawn a nymph there
        var coords = Transform(uid).Coordinates;
        var nymph = SpawnAtPosition(entityProto.ID, coords);

        if (HasComp<ZombieComponent>(args.OldBody)) // Zombify the new nymph if old one is a zombie // Starlight Edit: Target -> OldBody
            _zombie.ZombifyEntity(nymph);

        // The brain handler may already have moved the mind into this organ. Prefer it over the former body's
        // mind, including when a transplanted organ carries a donor's mind, then use the same policy as BrainSystem.
        if (comp.TransferMind == true)
        {
            var source = _mindSystem.TryGetMind(uid, out _, out _) ? uid : args.OldBody;
            if (_body.CanTransferMindOnOrganChange(source, nymph)
                && _mindSystem.TryGetMind(source, out var mindId, out var mind))
                _mindSystem.TransferTo(mindId, nymph, mind: mind);
        }

        // Delete the old organ
        QueueDel(uid);
    }
}
