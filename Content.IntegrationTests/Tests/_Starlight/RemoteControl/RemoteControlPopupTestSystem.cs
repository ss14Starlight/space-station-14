#nullable enable
using Content.Shared.Popups;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.RemoteControl;

public sealed class RemoteControlPopupTestSystem : EntitySystem
{
    public PopupEntityEvent? LastPopup { get; private set; }

    public override void Initialize() => SubscribeNetworkEvent<PopupEntityEvent>(OnPopupEntity);

    public void Clear() => LastPopup = null;

    private void OnPopupEntity(PopupEntityEvent ev) => LastPopup = ev;
}
