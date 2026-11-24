using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Verbs;

// ReSharper disable CheckNamespace
namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    private void AddCameraSetupVerb(EntityUid target, EntityUid user, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<SurveillanceCameraComponent>(target, out var camera)
            || !_uiSystem.HasUi(target, SurveillanceCameraSetupUiKey.Camera))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("surveillance-camera-setup"),
            Priority = int.MinValue / 2,
            DoContactInteraction = false,
            Act = () => RaiseLocalEvent(target, (object) new StationAiOpenCameraSetupEvent { User = user })
        });
    }
}
