using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.Flicker;

[RegisterComponent]
public sealed partial class FlickeringBulbComponent : Component
{
    /// <summary>
    /// Flicker while the bulb works, e.g. an old tube.
    /// </summary>
    [DataField]
    public ProtoId<LightFlickerPrototype>? Flicker;

    /// <summary>
    /// Flicker of a broken bulb in a powered fixture, normally dark.
    /// </summary>
    [DataField]
    public ProtoId<LightFlickerPrototype>? BrokenFlicker;
}
