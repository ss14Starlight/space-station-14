using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Hitscan.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanDamageFalloffComponent : Component
{
    [DataField]
    public float FullDamageDistance = 2f;

    [DataField]
    public float MinDamageDistance = 8f;

    [DataField]
    public float MinMultiplier = 0.25f;
}
