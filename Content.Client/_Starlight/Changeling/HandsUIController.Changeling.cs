using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared._Starlight.Changeling;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.UserInterface.Systems.Hands;

public sealed partial class HandsUIController
{
    private Texture? _shockHandSlotTexture;

    private void UpdateChangelingHandIcon(HandButton hand)
    {
        hand.ItemIcon.Visible = _playerHandsComponent != null &&
                                _player.LocalEntity is { } player &&
                                _handsSystem.TryGetHeldItem((player, _playerHandsComponent), hand.SlotName, out var held) &&
                                _entities.HasComponent<ChangelingShockHandComponent>(held.Value);
        if (!hand.ItemIcon.Visible)
            return;

        _shockHandSlotTexture ??= IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>(
            "/Textures/_Starlight/Interface/Actions/changeling_adaptations.rsi/shock-hand.png").Texture;
        hand.ItemIcon.Texture = _shockHandSlotTexture;
    }
}
