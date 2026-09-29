using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.Limbs;

[RegisterComponent, NetworkedComponent]
public sealed partial class RoundstartImplantableComponent : Component
{
    [DataField(required: true)]
    public int Cost;
}
