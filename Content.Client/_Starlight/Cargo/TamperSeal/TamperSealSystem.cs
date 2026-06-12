using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;

namespace Content.Client._Starlight.Cargo.TamperSeal;

public sealed class TamperSealSystem : SharedTamperSealSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TamperSealComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(EntityUid uid, TamperSealComponent component, AfterAutoHandleStateEvent args)
    {
        //Appearance.SetData(uid, TamperSealVisuals.Opened, component.Opened);
        //Appearance.SetData(uid, TamperSealVisuals.Violated, component.Violated);
    }
}
