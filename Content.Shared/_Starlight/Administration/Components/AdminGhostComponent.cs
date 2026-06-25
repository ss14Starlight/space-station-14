using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Administration.Components;

[RegisterComponent]
public sealed partial class AdminGhostComponent : Component
{
    [DataField] public EntProtoId ToggleAGhostHideActionId = "ActionToggleAGhostHide";
    [DataField] public ProtoId<TagPrototype> ToggleAGhostHideTag = "AdminGhostHidden";
    [ViewVariables] public EntityUid? ToggleAGhostHideActionEntity;
}
