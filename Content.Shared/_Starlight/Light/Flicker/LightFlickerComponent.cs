using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.Flicker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightFlickerComponent : Component
{
    /// <summary>
    /// How the fixture flickers right now, null when it is steady.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ProtoId<LightFlickerPrototype>? Profile;

    /// <summary>
    /// Replaces the bulb's own flicker while the fixture is lit normally, e.g. a failing maintenance light.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<LightFlickerPrototype>? Override;

    /// <summary>
    /// Client side: the profile the flicker below was started for.
    /// </summary>
    [ViewVariables]
    public ProtoId<LightFlickerPrototype>? Playing;

    /// <summary>
    /// Client side: when the light next toggles, or the next burst starts.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextToggle;

    /// <summary>
    /// Client side: toggles left in the current burst.
    /// </summary>
    [ViewVariables]
    public int TogglesLeft;

    /// <summary>
    /// Client side: whether the light is flipped from its normal state right now.
    /// </summary>
    [ViewVariables]
    public bool Toggled;
}
