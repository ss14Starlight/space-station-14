using System.ComponentModel;
using Content.Shared.Damage.Components;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight.TextToSpeech;

[Serializable, NetSerializable]
public sealed class TTSHeaderEvent : EntityEventArgs
{
    public ProtoId<RadioChannelPrototype>? Channel { get; set; }
    public Guid Id { get; set; }
    public TTSType Type { get; set; }
    public SoundSpecifier? Chime { get; set; }
    public NetEntity? SourceUid { get; set; }                               
    public float VolumeModifier { get; set; } = 1;
}

[Serializable, NetSerializable]
public sealed class TTSChunkEvent : EntityEventArgs
{
    public Guid Id { get; set; }
    public byte[] Data { get; set; } = [];
}

public enum TTSType
{
    System,
    IG,
    Mind,
    Radio,
    Announcement,
}
