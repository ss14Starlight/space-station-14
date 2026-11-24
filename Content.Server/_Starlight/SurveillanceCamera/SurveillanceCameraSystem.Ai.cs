using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem
{
    [SubscribeLocalEvent]
    private void OnStationAiOpenCameraSetup(EntityUid uid, SurveillanceCameraComponent component, StationAiOpenCameraSetupEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User)
            || !TryComp<StationAiWhitelistComponent>(uid, out var whitelist)
            || !whitelist.Enabled
            || !_userInterface.HasUi(uid, SurveillanceCameraSetupUiKey.Camera))
            return;

        OpenSetupInterface(uid, args.User, component);
    }
}
