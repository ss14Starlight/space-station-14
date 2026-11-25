using Content.Server.Silicons.StationAi;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem
{
    [Dependency] private StationAiSystem _stationAi = default!;
    [Dependency] private StationAiVisionSystem _stationAiVision = default!;

    [SubscribeLocalEvent]
    private void OnStationAiOpenCameraSetup(EntityUid uid, SurveillanceCameraComponent component, StationAiOpenCameraSetupEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User)
            || !TryComp<StationAiWhitelistComponent>(uid, out var whitelist)
            || !whitelist.Enabled
            || !_userInterface.HasUi(uid, SurveillanceCameraSetupUiKey.Camera)
            || !_stationAi.CanAccessGrid((args.User, null), Transform(uid).GridUid)
            || _stationAiVision.IsOutsideCameraViewCached(uid))
            return;

        OpenSetupInterface(uid, args.User, component);
    }
}
