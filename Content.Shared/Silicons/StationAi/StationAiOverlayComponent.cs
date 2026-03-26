using Content.Shared.Starlight.Antags.Abductor;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Handles the static overlay for station AI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool AllowCrossGrid;

    [DataField, AutoNetworkedField]
    public float Alfa = 1f;

    /// <summary>
    /// Starlight Addition!
    /// If non-null, will only show tiles with the specified tags. All tags must be present for the tile to be visible.
    /// Meant for views that should be selective with what is visible, like the xenobiology console camera.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> RequiredTags = new();
}
