using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;

namespace Content.Client._Starlight.Medical.Body.Systems;

public sealed partial class InternalsSystem : SharedInternalsSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InternalsComponent, AfterAutoHandleStateEvent>(OnInternalsAfterState);
    }

    private void OnInternalsAfterState(Entity<InternalsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.GasTankEntity != null && _ui.TryGetOpenUi(ent.Comp.GasTankEntity.Value, SharedGasTankUiKey.Key, out var bui))
        {
            bui.Update();
        }
    }
}
