using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[AutoGenerateComponentState]
public sealed partial class UmbraeComponent : VampireClassComponent
{
    [AutoNetworkedField]
    public bool CloakOfDarknessActive = false;

    public int CloakOfDarknessLoopId = 0;

    [DataField]
    public int BreakLightBloodThreshold = 300;

    [DataField]
    public float BreakLightRange = 8f;

    [DataField]
    public float CloakOfDarknessRevealRange = 4.5f;

    [DataField]
    public float CloakOfDarknessMinVisibility = -0.8f;

    [DataField]
    public float CloakOfDarknessMaxVisibility = 0.6f;

    [DataField]
    public TimeSpan CloakOfDarknessVisibilityUpdateInterval = TimeSpan.FromSeconds(0.15);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextCloakOfDarknessVisibilityUpdate;

    public bool CloakHadStealthComponent;

    public bool CloakPreviousStealthEnabled;

    public float CloakPreviousStealthVisibility = 1f;

    [AutoNetworkedField]
    public bool EternalDarknessActive = false;
    public EntityUid? EternalDarknessAuraEntity = null;
    [AutoNetworkedField]
    public bool ShadowBoxingActive = false;

    [AutoNetworkedField]
    public EntityUid? ShadowBoxingTarget = null;
    public TimeSpan? ShadowBoxingEndTime = null;
    public bool ShadowBoxingLoopRunning = false;
    public int EternalDarknessLoopId = 0;

    [AutoNetworkedField]
    public EntityUid? SpawnedShadowAnchorBeacon = null;

    [AutoPausedField]
    public TimeSpan? ShadowAnchorAutoReturnTime;

    public bool ShadowAnchorPlacementInProgress;
    public int ShadowAnchorLoopId;

    /// <summary>
    /// List of placed shadow snare traps
    /// </summary>
    public List<EntityUid> PlacedSnares = new();

    /// <summary>
    /// Maximum number of shadow snares that can be placed
    /// </summary>
    [DataField]
    public int MaxSnares = 3;
}
