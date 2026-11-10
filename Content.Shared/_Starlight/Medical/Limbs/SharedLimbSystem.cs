using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.Limbs;
public abstract partial class SharedLimbSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SLBodySystem _slBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WithAttachedBodyPartsComponent, MapInitEvent>(OnWithAttachedBodyPartsMapInit);

    }

    private void OnWithAttachedBodyPartsMapInit(Entity<WithAttachedBodyPartsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<SLBodyPartComponent>(ent, out var limb))
            return;

        foreach (var (slot, partProtoId) in ent.Comp.Parts)
        {
            if (!_prototypes.TryIndex(partProtoId, out var prototype))
                continue;

            var part = SpawnAtPosition(prototype.ID, Transform(ent).Coordinates);
            if (!TryComp<SLBodyPartComponent>(part, out var partComp))
            {
                Log.Error($"{ToPrettyString(ent)}: attached part {prototype.ID} is not a body part");
                QueueDel(part);
                continue;
            }

            var socketId = SharedBodySystem.GetSocketIdForLegacySlot(slot);
            if (!limb.Sockets.ContainsKey(socketId))
                _slBody.AddSocket((ent, limb), socketId, BodyPrefabPrototype.DefaultExternalSocket);

            if (!_slBody.TryAttachPart((ent, limb), socketId, (part, partComp)))
                QueueDel(part);
        }
    }
}
