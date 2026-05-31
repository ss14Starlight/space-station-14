using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeMutationPotionComponent : Component
{
    public const double MutationChangeAmount = 0.12;
}
