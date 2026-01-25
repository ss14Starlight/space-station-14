using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.TextToSpeech;
/// <summary>
/// Prototype represent TTS voices
/// </summary>
[Prototype("voice")]
public sealed partial class VoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("voice")]
    public int Voice { get; private set; }

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("sex", required: true)]
    public Sex Sex { get; private set; } = default!;

    [DataField("silicon")]
    public bool Silicon { get; private set; } = false;
}
