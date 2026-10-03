using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Roles;
using Robust.Shared.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;


    /// <summary>
    /// Spawns and attaches body parts defined in the starting gear prototype
    /// </summary>
    public void EquipBodyPartGear(EntityUid entity, IEquipmentLoadout? startingGear)
    {
        if (startingGear is null || startingGear.BodyParts.Count == 0)
            return;

        if (!TryComp<BodyComponent>(entity, out var body))
            return;

        var coords = _xformSystem.GetMapCoordinates(entity);

        foreach (var (slotName, entProto) in startingGear.BodyParts)
        {
            var spawned = Spawn(entProto, coords);

            if (!TryComp<BodyPartComponent>(spawned, out var partComp))
            {
                Del(spawned);
                continue;
            }

            if (!TryAttachLimb(entity, body, slotName, spawned, partComp))
                Del(spawned);
        }
    }

    /// <summary>
    /// Spawns and attaches organs defined in the starting gear prototype
    /// </summary>
    public void EquipOrganGear(EntityUid entity, IEquipmentLoadout? startingGear)
    {
        if (startingGear is null || startingGear.Organs.Count == 0)
            return;

        if (!TryComp<BodyComponent>(entity, out var body))
            return;

        var coords = _xformSystem.GetMapCoordinates(entity);

        foreach (var (slotName, organEntry) in startingGear.Organs)
        {
            var spawned = Spawn(organEntry.Proto, coords);

            if (!TryComp<OrganComponent>(spawned, out var organComp))
            {
                Del(spawned);
                continue;
            }

            if (!TryInsertOrgan(entity, body, slotName, spawned, organComp, organEntry.Replace))
                Del(spawned);
        }
    }

    private bool TryAttachLimb(
        EntityUid bodyEntity,
        BodyComponent body,
        string slotName,
        EntityUid partUid,
        BodyPartComponent partComp)
    {
        foreach (var (ownerUid, ownerPart) in _body.GetBodyChildren(bodyEntity, body))
        {
            if (!_body.TryCreatePartSlot(ownerUid, slotName, partComp.PartType, out var slot, ownerPart))
                continue;

            return _body.AttachPart(ownerUid, slot.Value, partUid, ownerPart, partComp);
        }

        return false;
    }

    private bool TryInsertOrgan(
        EntityUid bodyEntity,
        BodyComponent body,
        string slotName,
        EntityUid organUid,
        OrganComponent organComp,
        bool replace)
    {

        foreach (var (ownerUid, partComp) in _body.GetBodyChildren(bodyEntity, body))
        {
            if (!organHasKey(partComp, slotName))
                continue;

            if (replace)
            {
                var containerId = SharedBodySystem.GetOrganContainerId(slotName);

                if (_containers.TryGetContainer(ownerUid, containerId, out var container) 
                    && container.ContainedEntities.Count > 0)
                {
                    var preexistingOrganId = container.ContainedEntities[0];
                    if (!_body.RemoveOrgan(preexistingOrganId))
                        Log.Warning($"Failed to remove pre-existing organ in slot {slotName}.");

                    Del(preexistingOrganId);
                }
            }

            
            return _body.InsertOrgan(ownerUid, organUid, slotName, partComp, organComp);
        }
        
        return false;
    }

    private bool organHasKey(BodyPartComponent partComp, string key)
    {
        foreach (var (slotId, _) in partComp.Organs)
        {
            if (key == slotId)
                return true;
        }
        
        return false;
    }
}