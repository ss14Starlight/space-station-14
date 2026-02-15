using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class UmbraeComponent : VampireClassComponent
{
    [AutoNetworkedField]
    public bool CloakOfDarknessActive = false;

    public int CloakOfDarknessLoopId = 0;

    [DataField]
    public float CloakOfDarknessRevealRange = 4.5f;

    [DataField]
    public float CloakOfDarknessMinVisibility = -0.8f;

    [DataField]
    public float CloakOfDarknessMaxVisibility = 0.6f;

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
    public bool ShadowAnchorPlacementInProgress;
    public int ShadowAnchorLoopId;

    /// <summary>
    /// List of placed shadow snare traps
    /// </summary>
    [AutoNetworkedField]
    public List<EntityUid> PlacedSnares = new();

    /// <summary>
    /// Maximum number of shadow snares that can be placed
    /// </summary>
    [DataField]
    public int MaxSnares = 3;
}