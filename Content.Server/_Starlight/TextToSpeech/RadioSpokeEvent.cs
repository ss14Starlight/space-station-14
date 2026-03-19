using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Speech;
using Content.Shared.Radio;

namespace Content.Server.Starlight.TTS;

public sealed class RadioSpokeEvent : EntityEventArgs
{
    public RadioChannelPrototype Channel { get; set; } = null!;
    public EntityUid Source { get; set; }
    public SpeechMessage Message { get; set; } = null!; 

    public LanguagePrototype Language = null!;
    public bool SuppressTTS { get; set; } = false;
    public EntityUid[] Receivers { get; set; } = null!;
}
