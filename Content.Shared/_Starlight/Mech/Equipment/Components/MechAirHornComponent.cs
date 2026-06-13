using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Mech.Equipment.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechAirHornComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier HornSound = new SoundPathSpecifier("/Audio/Items/airhorn.ogg");

    [DataField, AutoNetworkedField]
    public float Range = 10f;
}
