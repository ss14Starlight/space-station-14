using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Humanoid;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Medical.Surgery.Components;

namespace Content.Server._Starlight.Medical.Limbs;
public sealed partial class LimbSystem : SharedLimbSystem
{
    [Dependency] private SLBodySystem _slBody = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidAppearanceSystem = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly EntProtoId _virtual = "PartVirtual";

    public bool AttachLimb(Entity<HumanoidAppearanceComponent> body, string slot, Entity<SLBodyPartComponent> part, Entity<SLBodyPartComponent> limb)
    {
        if (!TryAttachSLPart(part, slot, limb))
            return false;
        AddLimbVisual(body, limb);
        AddLimb(body, limb);
        return true;
    }

    public bool AttachItem(EntityUid body, string slot, Entity<SLBodyPartComponent> part, Entity<MetaDataComponent> item)
    {
        var marker = EnsureComp<CustomLimbMarkerComponent>(item);

        var virtualItem = Spawn(_virtual);
        var virtualBodyPart = EnsureComp<SLBodyPartComponent>(virtualItem);
        var virtualCustomLimb = EnsureComp<CustomLimbComponent>(virtualItem);
        _metadata.SetEntityName(virtualItem, item.Comp.EntityName);

        marker.VirtualPart = virtualItem;
        virtualCustomLimb.Item = item;

        virtualBodyPart.PartType = BodyParts.GetBodyPart(slot).ToString();
        Dirty(virtualItem, virtualBodyPart);

        if (!TryAttachSLPart(part, slot, virtualItem))
        {
            QueueDel(virtualItem);
            return false;
        }
        AddItemLimb(body, slot, item);
        AddItemHand(body, item, ItemHandId(slot));
        return true;
    }

    public void Amputatate(Entity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent> body, Entity<TransformComponent, MetaDataComponent, SLBodyPartComponent> limb)
    {
        if (!TryComp<SLBodyPartComponent>(limb, out var slLimb)
            || slLimb.ParentSocket is not { } socket
            || !_slBody.TryDetachPart((limb.Owner, slLimb), body.Comp1.Coordinates))
            return;

        var slotId = SharedBodySystem.GetLegacySlotName(socket.SocketId);
        if (TryComp<CustomLimbComponent>(limb, out var virtualLimb))
            AmputateItemLimb(body.Owner, limb, slotId, virtualLimb);
        else
        {
            RemoveLimbVisual(body, limb);
            RemoveLimb(body, limb);
        }
    }

    private bool TryAttachSLPart(EntityUid parentId, string slot, EntityUid childId)
    {
        if (!TryComp<SLBodyPartComponent>(parentId, out var parent) || !TryComp<SLBodyPartComponent>(childId, out var child))
            return false;

        var socketId = SharedBodySystem.GetSocketIdForLegacySlot(slot);
        if (!parent.Sockets.ContainsKey(socketId))
            _slBody.AddSocket((parentId, parent), socketId, BodyPrefabPrototype.DefaultExternalSocket);

        return _slBody.TryAttachPart((parentId, parent), socketId, (childId, child));
    }

    private void AddItemLimb(EntityUid body, string slot, Entity<MetaDataComponent> item)
    {
        var layer = VisualLayers.GetLayer(slot);
        var vizualizer = EnsureComp<CustomLimbVisualizerComponent>(body);
        vizualizer.Layers[layer] = GetNetEntity(item);
        Dirty(body, vizualizer);
    }

    private void AmputateItemLimb(EntityUid body, Entity<TransformComponent, MetaDataComponent, SLBodyPartComponent> limb, string slotId, CustomLimbComponent virtualLimb)
    {
        RemoveItemLimb(body, virtualLimb.Item, ItemHandId(slotId));

        var vizualizer = EnsureComp<CustomLimbVisualizerComponent>(body);

        var layer = VisualLayers.GetLayer(slotId);
        vizualizer.Layers.Remove(layer);
        Dirty(body, vizualizer);
        QueueDel(limb.Owner);
    }

    private static string ItemHandId(string slot) => "item_limb_" + slot;

    private void AddItemHand(EntityUid bodyId, EntityUid itemId, string handId)
    {
        if (!TryComp<HandsComponent>(bodyId, out var hands))
            return;

        if (!itemId.IsValid())
        {
            Log.Debug("no valid item");
            return;
        }

        _hands.AddHand((bodyId,hands), handId, HandLocation.Middle);
        _hands.DoPickup(bodyId, handId, itemId, hands);
        EnsureComp<UnremoveableComponent>(itemId);
    }

    private void RemoveItemLimb(EntityUid bodyId, EntityUid itemId, string handId)
    {
        if (!bodyId.IsValid() || !itemId.IsValid()) return;

        RemComp<UnremoveableComponent>(itemId);
        _hands.DoDrop(itemId, handId);
        _hands.RemoveHand(bodyId, handId);
    }
}
