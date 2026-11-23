using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.MeteorShield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeteorShieldSatelliteComponent : Component
{
    /// <summary>
    /// Range in tiles at which meteors are destroyed.
    /// </summary>
    [DataField]
    public float Range = 14f;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(0.2);

    [DataField]
    public EntProtoId BeamPrototype = "MeteorShieldBeam";

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// Number shown as MSAT-N. The server assigns it on spawn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Designation;

    /// <summary>
    /// When the server next looks for meteors in range.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextCheck;
}
