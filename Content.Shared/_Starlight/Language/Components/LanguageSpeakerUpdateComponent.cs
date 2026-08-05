using Content.Shared._Starlight.Language.Systems;

namespace Content.Shared._Starlight.Language.Components;

[RegisterComponent, Access(typeof(SharedLanguageSystem))]
public sealed partial class LanguageSpeakerUpdateComponent : Component
{
    public uint TargetTick;
}
