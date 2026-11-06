using Content.Server.Ame.Components;
using Content.Server.Audio;
using Content.Server.Power.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Ame.EntitySystems;

public sealed partial class AmeControllerSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, AmeControllerComponent controller)
    {
        var powered = !TryComp<ApcPowerReceiverComponent>(uid, out var receiver) || receiver.Powered;
        _ambientSound.SetAmbience(uid, controller.Injecting && powered);
    }
}
