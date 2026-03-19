using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Damage;

[RegisterComponent, NetworkedComponent]
public sealed partial class UniversalHealModifierComponent : Component
{
    [DataField]
    public float Modifier = 1.0f;
}