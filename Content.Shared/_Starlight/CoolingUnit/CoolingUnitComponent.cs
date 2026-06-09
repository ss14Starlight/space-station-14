using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CoolingUnit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CoolingUnitComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionToggleCoolingUnit";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    /// Max Cooling by Sec.
    /// </summary>
    [DataField]
    public float MaxCooling = 12f;
}
