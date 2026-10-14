using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Administration.Components;

[RegisterComponent]
public sealed partial class AdminGhostComponent : Component
{
    [DataField] public EntProtoId ToggleAGhostHideActionId = "ActionToggleAGhostHide";
    [ViewVariables] public bool HiddenFromNonAdminGhosts;
    [ViewVariables] public EntityUid? ToggleAGhostHideActionEntity;
}
