using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GargantuaComponent : VampireClassComponent
{
    /// <summary>
    ///     Whether Overwhelming Force toggle is active
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OverwhelmingForceActive;

    /// <summary>
    ///     Whether vampire is currently charging
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsCharging;

    /// <summary>
    ///     Current charge direction as vector
    /// </summary>
    public Vector2 ChargeDirectionVector;

    [DataField]
    public TimeSpan BloodSwellShootPopupCooldown = TimeSpan.FromSeconds(1f);
    [DataField]
    public TimeSpan? BloodSwellShootNextPopupTime;

    [DataField]
    public EntityUid? BloodSwellShootLastGun;
}
