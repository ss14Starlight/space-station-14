using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Power.BluespaceHarvester;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BluespaceHarvesterComponent : Component
{

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);
    [DataField, AutoPausedField]
    public TimeSpan LastUpdate;

    [DataField]
    public List<float> LevelPowerDraw = new() // Power draw required per level
    {
        1_000f, // 1      | 1 KW
        5_000f, // 2      | 5 KW
        50_000f, // 3     | 50 KW
        100_000f, // 4    | 100 KW
        500_000f, // 5    | 500 KW
        1_000_000f, // 6  | 1 MW
        5_000_000f, // 7  | 5 MW
        10_000_000f, // 8 | 10 MW
        25_000_000f, //9  | 25 MW
        100_000_000f //10 | 100 MW
    };

    [DataField] public float PointsPerLevel = 2f;

    [DataField] public float PointsPerMegawatt = 8f;

    [DataField] public int DesiredLevel;

    [DataField] public int Points; // used for buying stuff
    [DataField] public int TotalPoints; // Will be used for station goals later

    // Dangerous mode settings
    [DataField] public int DangerousLevelThreshold = 7;
    [DataField] public float BasePortalChancePerSecond = 0.005f; // 0.5% per second at level 7
    [DataField] public float PortalChancePerLevelAboveThreshold = 0.01f; // +1% per level above threshold
    [DataField] public EntProtoId PortalPrototype = "BluespaceHarvesterPortal";
    [DataField] public List<EntProtoId> PortalMobPrototypes = new()
    {
        "MobBluespaceHarvesterMiGo",
        "MobBluespaceHarvesterBlankBody",
        "MobBluespaceHarvesterOtherthing"
    };

    [DataField] public int MinMobsPerPortal = 2;
    [DataField] public int MaxMobsPerPortal = 5;

    [DataField] public float PortalMinDistance = 3f;

    [DataField] public float PortalMaxDistance = 5f;

    [DataField] public float PortalBlockDuration = 30f;

    [ViewVariables(VVAccess.ReadOnly)]
    public int CurrentLevel;

    [ViewVariables(VVAccess.ReadOnly)]
    public float PointAccumulator; // for fractional point accumulation

    [ViewVariables(VVAccess.ReadOnly)]
    public float PortalAccumulator; // for portal spawn timing

    /// <summary>
    /// Whether the harvester is currently blocked due to an active portal
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsBlocked;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActivePortal;

    public int LastUiCurrentLevel;
    public int LastUiDesiredLevel;
    public int LastUiPoints;
    public int LastUiTotalPoints;
    public float LastUiCurrentPower;
    public float LastUiNextPower;
    public float LastUiNetworkSupply;
    public bool LastUiIsBlocked;
}

