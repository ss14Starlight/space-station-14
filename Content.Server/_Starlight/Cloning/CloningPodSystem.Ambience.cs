using Content.Server.Audio;
using Content.Shared.Cloning;

// ReSharper disable once CheckNamespace
namespace Content.Server.Cloning;

public sealed partial class CloningPodSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, CloningPodStatus status)
        => _ambientSound.SetAmbience(uid, status == CloningPodStatus.Cloning);
}
