using Content.Shared._Starlight.Movement.Components;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Audio;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    private static readonly string[] _footstepLayerSlots = ["shoes", "outerClothing"];

    private void PlayFootstepLayers(EntityUid uid, EntityUid user, AudioParams stepParams)
    {
        foreach (var slot in _footstepLayerSlots)
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item) ||
                !TryComp(item, out FootstepLayerComponent? layer) ||
                layer.RequireActive && !_itemToggle.IsActivated(item.Value))
                continue;

            var audioParams = layer.Sound.Params
                .WithVolume(layer.Sound.Params.Volume + stepParams.Volume)
                .WithVariation(layer.Sound.Params.Variation ?? stepParams.Variation);

            _audio.PlayPredicted(layer.Sound, uid, user, audioParams);
        }
    }
}
