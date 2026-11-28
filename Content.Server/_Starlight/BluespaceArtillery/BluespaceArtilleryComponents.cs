using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.BluespaceArtillery;

public enum BluespaceArtilleryPart : byte
{
    Generator,
    Fusor,
    Bore,
}

[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryPartComponent : Component
{
    [DataField(required: true)]
    public BluespaceArtilleryPart Part;
}

[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryComponent : Component
{
    /// <summary>
    /// Firing direction in grid space. Only east and west are supported by the sprite.
    /// </summary>
    [DataField]
    public Direction Facing = Direction.East;

    /// <summary>
    /// Distance in tiles from the cannon's centre to the tile where the shell appears.
    /// </summary>
    [DataField]
    public float MuzzleDistance = 5f;

    /// <summary>
    /// Energy needed for one shot, in joules.
    /// </summary>
    [DataField]
    public float ChargeEnergy = 180_000_000f;

    /// <summary>
    /// Power drawn from the cable while charging, in watts.
    /// </summary>
    [DataField]
    public float DrawRate = 3_000_000f;

    /// <summary>
    /// Time after a shot before the cannon can fire again.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [DataField]
    public EntProtoId ShellPrototype = "BluespaceArtilleryShell";

    /// <summary>
    /// Shell speed in tiles per second.
    /// </summary>
    [DataField]
    public float ShellSpeed = 25f;

    /// <summary>
    /// How long the shell flies before it vanishes and the explosion happens at the target.
    /// </summary>
    [DataField]
    public TimeSpan ShellLifetime = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Explosion type which will be caused at target place.
    /// </summary>
    [DataField]
    public string ExplosionType = "Default";

    /// <summary>
    /// Intensity of explosion which will be caused at target place.
    /// </summary>
    [DataField]
    public float TotalIntensity = 3000f;

    /// <summary>
    /// Slope of explosion which will be caused at target place.
    /// </summary>
    [DataField]
    public float Slope = 4f;

    /// <summary>
    /// Max intensity per tile for explosion which will be caused at target place.
    /// </summary>
    [DataField]
    public float MaxTileIntensity = 60f;

    /// <summary>
    /// Sound player by BSA when fired.
    /// </summary>
    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    /// <summary>
    /// Effect spawned at the muzzle when the cannon fires.
    /// </summary>
    [DataField]
    public EntProtoId MuzzleFlash = "BluespaceArtilleryMuzzleFlash";

    [DataField]
    public Color LightColor = Color.FromHex("#7fe7ff");

    /// <summary>
    /// Light color while the barrel cools down after a shot.
    /// </summary>
    [DataField]
    public Color CooldownLightColor = Color.FromHex("#ff7a40");

    /// <summary>
    /// Sound played by BSA when charged.
    /// </summary>
    [DataField]
    public SoundSpecifier ChargedSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    /// <summary>
    /// Stored energy in joules.
    /// </summary>
    [ViewVariables]
    public float Stored;

    /// <summary>
    /// Determines if BSA is charging right now.
    /// </summary>
    [ViewVariables]
    public bool Charging;

    /// <summary>
    /// Relative time, determines when BSA cooldown ends.
    /// </summary>
    [ViewVariables]
    public TimeSpan CooldownEnd;

    /// <summary>
    /// Link to BSA console.
    /// </summary>
    [ViewVariables]
    public EntityUid? Console;
}

[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryConsoleComponent : Component
{
    [DataField]
    public EntProtoId EastPrototype = "BluespaceArtilleryEast";

    [DataField]
    public EntProtoId WestPrototype = "BluespaceArtilleryWest";

    [ViewVariables]
    public EntityUid? Cannon;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public string? Notice;

    [ViewVariables]
    public EntityUid? Operator;

    [ViewVariables]
    public EntityUid? AlertedTarget;
}

[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryShellComponent : Component
{
    /// <summary>
    /// Damage dealt to each entity the shell passes through.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();

    [ViewVariables]
    public EntityUid? Cannon;

    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Where the target was at the moment of firing, used when the target is gone by the time the shell lands.
    /// </summary>
    [ViewVariables]
    public MapCoordinates TargetCoordinates;

    [ViewVariables]
    public TimeSpan ExplodeAt;

    [ViewVariables]
    public string ExplosionType = "Default";

    [ViewVariables]
    public float TotalIntensity;

    [ViewVariables]
    public float Slope;

    [ViewVariables]
    public float MaxTileIntensity;

    [ViewVariables]
    public readonly HashSet<EntityUid> Hit = [];
}

[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryTestSiteComponent : Component
{
    /// <summary>
    /// The station whose goal created this test site.
    /// </summary>
    [ViewVariables]
    public EntityUid? Station;

    [ViewVariables]
    public bool Hit;
}

/// <summary>
/// Keeps the bluespace artillery off a grid or map. Put it on a grid, a map or a station entity. On a station it protects every grid of that station.
/// Beacons there are hidden from the target list, and shells that land there do not explode.
/// </summary>
[RegisterComponent, Access(typeof(BluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryProtectedComponent : Component;
