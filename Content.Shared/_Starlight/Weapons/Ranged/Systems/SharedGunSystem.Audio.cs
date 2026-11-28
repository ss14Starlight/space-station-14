using Content.Shared._Starlight.Computers.RemoteControl;
using Robust.Shared.Audio;

// ReSharper disable CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected void PlayPredictedAudio(SoundSpecifier? sound, EntityUid source, EntityUid? user, AudioParams? audioParams = null)
        => Audio.PlayPredicted(sound, source, EntityManager.GetPredictedAudioUser(user), audioParams);
}
