using Content.Shared.Starlight.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Radio.Systems;

/// <summary>
/// This system handles playing radio chime sounds on the client side when radio messages are received.
/// </summary>
public sealed class RadioChimeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public bool IsMuted = false;
    private bool _ttsEnabled = false;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.TTSClientEnabled, x => _ttsEnabled = x, true);
        Subs.CVar(_cfg, StarlightCCVars.RadioChimeMuted, x => IsMuted = x, true);
    }

    public void PlayChime(SoundSpecifier? chimeSound)
    {
        if (chimeSound is not SoundSpecifier chime
            || IsMuted
            || _ttsEnabled)
            return;

        _audio.PlayGlobal(_audio.ResolveSound(chime), Filter.Local(), true, AudioParams.Default.WithVolume(-10f));
    }
}
