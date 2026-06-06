

namespace Content.Shared._Starlight.PoweredShields.Components;

/// <summary>
/// This component goes on an item that you want to use to consume power to block damage directly to the user
/// </summary>
/// [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoweredShieldComponent : Component
{
    /// <summary>
    /// How much energy will be spent from the battery per unit of damage taken by the shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageEnergyDraw = 10f;

    /// <summary>
    /// How much energy will be spent from the battery per reflect performed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReflectEnergyDraw = 20f;
}
