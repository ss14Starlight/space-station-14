using Content.Shared.Power.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Weapons.Ranged.Components;

/// <summary>
/// Ammo provider that uses electric charge from a battery to provide ammunition to a weapon.
/// Works in combination with <see cref="BatteryComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class MechAmmoProviderComponent : AmmoProviderComponent
{
    /// <summary>
    /// The projectile or hitscan entity to spawn when firing.
    /// </summary>
    [DataField("proto", required: true)]
    public EntProtoId Prototype;

    /// <summary>
    /// How much charge it costs to fire once, in watts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FireCost = 100;

    /// <summary>
    /// The mech this piece of equipment is installed in.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Mech {get; set;}
}
