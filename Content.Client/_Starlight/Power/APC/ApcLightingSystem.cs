using Content.Client.Power.APC;
using Content.Shared._Starlight.Light.NightLighting;
using Content.Shared.APC;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Power.APC;

public sealed partial class ApcLightingSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnAfterHandleState(Entity<ApcLightingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<ApcBoundUserInterface>(ent.Owner, ApcUiKey.Key, out var bui))
            bui.UpdateLighting();
    }
}
