using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Interactable pad that spawns a single tutorial anomaly for science practice.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialAnomalySpawnerComponent : Component
{
    [DataField]
    public EntProtoId AnomalyProto = "TutorialAnomaly";

    /// <summary>
    /// Local offset from the pad where the anomaly appears.
    /// </summary>
    [DataField]
    public Vector2 Offset = new(1.5f, 0f);

    [DataField, AutoNetworkedField]
    public bool Spawned;

    /// <summary>
    /// Spawned anomaly uid (for sensors / cleanup).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SpawnedAnomaly;
}
