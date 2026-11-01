using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Trigger.Components.Effects;

/// <summary>
/// Spawns a storage entity (like a bluespace anomaly core) and moves the target's worn and held gear into it on trigger.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeaveItemsOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField]
    public EntProtoId Proto = "AnomalyCoreBluespaceBelongings";
}
