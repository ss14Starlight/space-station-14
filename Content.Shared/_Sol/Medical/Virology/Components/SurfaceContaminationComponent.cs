using Robust.Shared.GameStates;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Pathogen load on an item, surface, food, or piece of PPE.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurfaceContaminationComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<PathogenContaminationEntry> Contaminants = new();

    /// <summary>
    /// True when this is ordinary surgical dirt / used state without a known pathogen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsDirty;
}
