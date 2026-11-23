using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.MeteorShield;

[RegisterComponent, Access(typeof(MeteorShieldSystem))]
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

    [ViewVariables]
    public bool Active;

    /// <summary>
    /// Number shown as MSAT-N. Assigned on spawn.
    /// </summary>
    [ViewVariables]
    public int Designation;

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>
/// Lists the satellites around the station, switches them and shows how much of the station they cover.
/// </summary>
[RegisterComponent, Access(typeof(MeteorShieldSystem))]
public sealed partial class MeteorShieldConsoleComponent : Component;
