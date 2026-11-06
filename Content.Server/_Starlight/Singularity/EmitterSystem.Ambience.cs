using Content.Server.Audio;
using Content.Shared.Singularity.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Singularity.EntitySystems;

public sealed partial class EmitterSystem
{
    [Dependency] private AmbientSoundSystem _ambientSound = default!;

    private void UpdateAmbience(EntityUid uid, EmitterComponent component)
        => _ambientSound.SetAmbience(uid, component.IsOn && component.IsPowered);
}
