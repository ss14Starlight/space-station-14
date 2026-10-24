using Content.Client._Starlight.Audio;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Client.Audio;

public sealed partial class ClientGlobalSoundSystem
{
    [Dependency] private SoundCategorySystem _soundCategory = default!;

    private const int MaxAdminSounds = 3;

    private const float MinAdminGain = 0.0001f;

    private float _adminGain = 1f;

    private readonly Dictionary<EntityUid, float> _adminBaseVolumes = new();

    private void InitializeAdminVolume()
        => Subs.CVar(_cfg, StarlightCCVars.AdminSoundsVolume, SetAdminGain, true);

    private void PlayAdminSoundCapped(AdminSoundEvent soundEvent)
    {
        if (_adminGain <= MinAdminGain)
            return;

        _adminAudio.RemoveAll(stream => stream is not { } uid || !Exists(uid));

        while (_adminAudio.Count >= MaxAdminSounds)
        {
            if (_adminAudio[0] is { } oldest)
                _adminBaseVolumes.Remove(oldest);

            _audio.Stop(_adminAudio[0]);
            _adminAudio.RemoveAt(0);
        }

        var audioParams = soundEvent.AudioParams ?? AudioParams.Default;
        var baseVolume = audioParams.Volume;

        using var exempt = _soundCategory.Exempt();
        var stream = _audio.PlayGlobal(soundEvent.Specifier, Filter.Local(), false,
            audioParams.WithVolume(baseVolume + SharedAudioSystem.GainToVolume(_adminGain)));

        if (stream is not { } played)
            return;

        _adminBaseVolumes[played.Entity] = baseVolume;
        _adminAudio.Add(played.Entity);
    }

    private void SetAdminGain(float gain)
    {
        _adminGain = gain;

        var volume = SharedAudioSystem.GainToVolume(MathF.Max(gain, MinAdminGain));
        foreach (var (uid, baseVolume) in _adminBaseVolumes)
        {
            if (Exists(uid))
                _audio.SetVolume(uid, baseVolume + volume);
        }
    }
}
