using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weather;

[RegisterComponent, NetworkedComponent]
public sealed partial class ParallaxStatusEffectComponent : Component
{
    [DataField(required: true)]
    public string Parallax = default!;
}