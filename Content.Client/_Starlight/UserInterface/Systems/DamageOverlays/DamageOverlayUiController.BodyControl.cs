using Content.Client._Starlight.BodyControl;
using Content.Shared.Mobs.Components;

// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Systems.DamageOverlays;

public sealed partial class DamageOverlayUiController
{
    private EntityUid? DamageUiEntity => EntityManager.System<BodyControlViewSystem>().Entity;

    private void InitializeBodyControlView()
        => SubscribeLocalEvent<BodyControlViewChangedEvent>(OnControlViewChanged);

    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
        if (args.Current is not { } body || !EntityManager.TryGetComponent<MobStateComponent>(body, out var state))
            return;
        UpdateOverlays(body, state);
        _overlayManager.AddOverlay(_overlay);
    }
}
