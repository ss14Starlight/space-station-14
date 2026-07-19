using Robust.Shared.GameStates;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Tracks active pathogen infections on a living entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenCarrierComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ActivePathogenInfection> Infections = new();
}
