using Content.Server.Ame.Components;
using Content.Server.Audio;

// ReSharper disable once CheckNamespace
namespace Content.Server.Ame.EntitySystems;

public sealed partial class AmeControllerSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, AmeControllerComponent controller)
        => _ambientSound.SetAmbience(uid, controller.Injecting);
}
