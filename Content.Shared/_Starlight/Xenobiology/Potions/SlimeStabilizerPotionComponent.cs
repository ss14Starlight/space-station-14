using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeStabilizerPotionComponent : Component
{
    public const double MutationChangeAmount = -0.15;
}