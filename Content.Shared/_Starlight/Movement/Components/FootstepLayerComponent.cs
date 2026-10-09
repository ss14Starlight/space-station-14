using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Movement.Components;

[RegisterComponent]
public sealed partial class FootstepLayerComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public bool RequireActive;
}
