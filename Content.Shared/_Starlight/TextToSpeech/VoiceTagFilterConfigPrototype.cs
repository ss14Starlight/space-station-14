using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Starlight.TextToSpeech;

[Prototype]
public sealed partial class VoiceTagFilterConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int MaxPresentedTags { get; private set; } = 25;

    [DataField]
    public HashSet<ProtoId<VoiceTagPrototype>> ExplicitlyIncludedTags { get; private set; } = new();

    [DataField]
    public HashSet<ProtoId<VoiceTagPrototype>> ExcludedTags { get; private set; } = new();
}
