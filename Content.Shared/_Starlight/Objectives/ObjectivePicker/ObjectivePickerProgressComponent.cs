using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Availability of additional objectives for this mind's owning session.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObjectivePickerProgressComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool CanPickMore;

    public override bool SessionSpecific => true;
}
