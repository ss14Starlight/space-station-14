using Content.Shared._Starlight.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Starlight.TTS;

public sealed class AnnouncementSpokeEvent : EntityEventArgs
{
    public Filter Receivers { get; set; } = null!;
    public NetEntity? SourceUid { get; set; }
    public NetEntity? SpeakerUid { get; set; }
    public SpeechMessage Message { get; set; } = null!;
    public SoundSpecifier? AnnouncementSound { get; set; } = null!;
}
