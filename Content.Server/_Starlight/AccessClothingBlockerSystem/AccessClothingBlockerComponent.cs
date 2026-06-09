using Robust.Shared.Audio;

namespace Content.Server._Starlight.FactionClothingBlockerSystem;

[RegisterComponent]
public sealed partial class AccessClothingBlockerComponent : Component
{
    [DataField("access", required: false)]
    public string? Access = null;

    [DataField("beepSound")]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");
}
