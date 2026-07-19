using Robust.Shared.GameStates;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Marks a vial as usable for Sol blood testing / centrifuge panels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CentrifugeCompatibleBloodVialComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? SourceEntity;

    [DataField, AutoNetworkedField]
    public bool PanelReady;
}
