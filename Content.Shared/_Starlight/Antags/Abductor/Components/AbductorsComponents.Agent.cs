using Content.Shared._Starlight.Antags.Abductor.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Starlight.Antags.Abductor.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorAgentComponent : Component
{
    [DataField("position"), AutoNetworkedField]
    public EntityCoordinates? SpawnPosition;

    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public EntityUid? Scientist;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorVestComponent : Component
{
    [DataField, AutoNetworkedField]
    public AbductorArmorModeType CurrentState = AbductorArmorModeType.Stealth;
}
