using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Radio.Components
{
    [RegisterComponent]
    public sealed partial class RadioChimeComponent : Component
    {
        [DataField]
        public SoundSpecifier? Sound;
    }
}
