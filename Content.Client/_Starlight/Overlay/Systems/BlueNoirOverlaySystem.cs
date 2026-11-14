using Content.Client._Starlight.Overlay.Overlays;
using Content.Client.Overlays;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.Overlay.Systems;

public sealed partial class BlueNoirOverlaySystem : EquipmentHudSystem<BlueNoirOverlayComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    private BlueNoirOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<BlueNoirOverlayComponent> component)
    {
        base.UpdateInternal(component);
        _overlayMan.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlayMan.RemoveOverlay(_overlay);
    }
}
