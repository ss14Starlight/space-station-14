using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sound;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HearingPressureComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [ViewVariables, AutoNetworkedField]
    public float Pressure = Atmospherics.OneAtmosphere;

    [ViewVariables, AutoNetworkedField]
    public bool SealedHelmet;

    public const float Step = 5f;
}
