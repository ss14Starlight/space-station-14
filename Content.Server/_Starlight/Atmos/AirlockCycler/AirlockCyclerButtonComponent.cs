using Robust.Shared.Audio;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

[RegisterComponent, Access(typeof(AirlockCyclerSystem))]
public sealed partial class AirlockCyclerButtonComponent : Component
{
    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/lightswitch.ogg");
}
