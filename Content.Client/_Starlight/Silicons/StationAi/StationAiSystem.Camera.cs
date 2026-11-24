using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private readonly ResPath _cameraSetupIcon = new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png");

    [SubscribeLocalEvent]
    private void OnCameraSetupGetRadial(Entity<SurveillanceCameraComponent> ent, ref GetStationAiRadialEvent args)
    {
        if (!TryComp<StationAiWhitelistComponent>(ent.Owner, out var whitelist)
            || !whitelist.Enabled
            || !_userInterface.HasUi(ent.Owner, SurveillanceCameraSetupUiKey.Camera))
            return;

        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Texture(_cameraSetupIcon),
            Tooltip = Loc.GetString("surveillance-camera-setup"),
            Event = new StationAiOpenCameraSetupEvent()
        });
    }
}
