using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Administration.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AdminGhostComponent : Component
{
    [DataField] public EntProtoId ToggleAGhostHideActionId = "ActionToggleAGhostHide";
    [ViewVariables, AutoNetworkedField] public bool HiddenFromNonAdminGhosts;
    [ViewVariables] public EntityUid? ToggleAGhostHideActionEntity;
}
