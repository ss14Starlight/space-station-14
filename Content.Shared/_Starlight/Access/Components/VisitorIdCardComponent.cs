using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VisitorIdCardComponent : Component
{
    [DataField, AutoNetworkedField] private ProtoId<JobIconPrototype> _jobIcon;
    [DataField, AutoNetworkedField] public string VisitorType
    {
        set => _jobIcon = new ProtoId<JobIconPrototype>(value); get => _jobIcon.ToString();
    }

    [DataField, AutoNetworkedField] public bool AccessSet;
}