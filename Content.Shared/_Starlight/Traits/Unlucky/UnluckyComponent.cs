using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Traits.Unlucky;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnluckyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float TopplingChance = 0.1f;
}
