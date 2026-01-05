using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.BreathOrgan.Components;

/// <summary>
/// A component handling the gas tank integrated organ
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganBreathToolComponent : Component
{
    /// <summary>
    /// when true, automatically start with internals toggled on (useful for vox to not die)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartActivated = false;

    /// <summary>
    /// Action used for toggling internals.
    /// </summary>
    [DataField]
    public EntProtoId ToggleAction = "ActionToggleInternalsOrgan";

    /// <summary>
    /// Action used for opening the gas tank UI.
    /// </summary>
    [DataField]
    public EntProtoId ViewGasTankAction = "ActionOpenOrganGasTankUI";

    [DataField, AutoNetworkedField]
    public EntityUid? ViewGasTankActionEntity;
}

