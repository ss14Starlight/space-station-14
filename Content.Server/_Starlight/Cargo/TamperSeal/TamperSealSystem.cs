using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;

namespace Content.Server._Starlight.Cargo.TamperSeal;

public sealed class TamperSealSystem : SharedTamperSealSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TamperSealComponent, ComponentStartup>(OnTamperSealStartup);
        SubscribeLocalEvent<TamperSealComponent, ComponentShutdown>(OnTamperSealShutdown);
    }

    private void OnTamperSealStartup(EntityUid uid, TamperSealComponent component, ComponentStartup args)
    {
        Appearance.SetData(uid, TamperSealVisuals.Opened, false);
        Appearance.SetData(uid, TamperSealVisuals.Violated, false);
    }

    private void OnTamperSealShutdown(EntityUid uid, TamperSealComponent component, ComponentShutdown args)
    {
        Appearance.RemoveData(uid, TamperSealVisuals.Opened);
        Appearance.RemoveData(uid, TamperSealVisuals.Violated);
    }
}
