using System.Linq;
using Content.Client.Inventory;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Sprite;

/// <summary>
/// Works out which of an entity's sprite layers belong to worn clothing and held items.
/// Everything else is the entity's own sprite (body, markings, base layers...).
/// </summary>
/// <remarks>
/// Is this bad? Yes. Yes it is.
/// </remarks>
public sealed partial class SpriteLayerGroupsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = null!;

    public void Collect(EntityUid uid, SpriteComponent sprite, HashSet<int> clothing, HashSet<int> held)
    {
        clothing.Clear();
        held.Clear();

        if (TryComp<InventorySlotsComponent>(uid, out var slots))
            foreach (var key in slots.VisualLayerKeys.Values.SelectMany(keys => keys))
                if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                    clothing.Add(index);

        if (!TryComp<HandsComponent>(uid, out var hands)) return;
        foreach (var key in hands.RevealedLayers.Values.SelectMany(keys => keys))
            if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                held.Add(index);
    }

    public void RemoveAttachmentLayers(EntityUid owner, Entity<SpriteComponent> sprite)
    {
        if (TryComp<InventorySlotsComponent>(owner, out var slots))
            foreach (var key in slots.VisualLayerKeys.Values.SelectMany(keys => keys))
                _sprite.RemoveLayer(sprite.AsNullable(), key, false);

        if (!TryComp<HandsComponent>(owner, out var hands)) return;
        foreach (var key in hands.RevealedLayers.Values.SelectMany(keys => keys))
            _sprite.RemoveLayer(sprite.AsNullable(), key, false);
    }

    public void ForgetAttachmentLayers(EntityUid owner)
    {
        if (TryComp<InventorySlotsComponent>(owner, out var slots))
            foreach (var keys in slots.VisualLayerKeys.Values)
                keys.Clear();

        if (!TryComp<HandsComponent>(owner, out var hands)) return;
        foreach (var keys in hands.RevealedLayers.Values)
            keys.Clear();
    }
}
