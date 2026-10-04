using Content.Server.Audio;
using Content.Server.ParticleAccelerator.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.ParticleAccelerator.EntitySystems;

public sealed partial class ParticleAcceleratorSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, ParticleAcceleratorControlBoxComponent comp)
        => _ambientSound.SetAmbience(uid, comp.Powered);
}
