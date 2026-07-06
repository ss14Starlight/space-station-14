using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Deathmatch;

[RegisterComponent, NetworkedComponent]
public sealed partial class DeathmatchActionComponent : Component;

[Virtual]
public partial class CreateWeaponEvent : InstantActionEvent
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId WeaponPrototype = "WeaponMeleeToolboxRobustDeathmatch";
}
