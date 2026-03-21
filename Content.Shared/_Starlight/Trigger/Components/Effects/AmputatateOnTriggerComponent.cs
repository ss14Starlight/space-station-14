using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Trigger.Components.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmputatateOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField]
    public bool TargetContainer;
    
    [DataField, AutoNetworkedField]
    public List<string> Parts;
}
