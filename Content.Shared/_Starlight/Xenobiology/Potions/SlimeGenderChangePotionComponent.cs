using Robust.Shared.Enums;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeGenderChangePotionComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Gender? Gender;
}