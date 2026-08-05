using Content.Shared.Inventory;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical;

/// <summary>
/// This is used for defibrillators intended to be equipped, like gloves that can shock people.
/// It grants an action when equipped to the specified slot.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WearableDefibrillatorComponent : Component
{
    /// <summary>
    /// The action it will grant.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionDefib";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// What slot the defib will give an action when equipped in.
    /// </summary>
    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.GLOVES;
}

/// <summary>
/// Event for the defib action.
/// </summary>
public sealed partial class DefibActionEvent : EntityTargetActionEvent { }
