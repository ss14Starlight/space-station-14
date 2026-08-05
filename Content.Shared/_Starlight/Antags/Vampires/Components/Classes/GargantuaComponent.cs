using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
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

    public float ChargeSpeed;

    public float ChargeCreatureDamage;

    public float ChargeCreatureThrowDistance;

    public float ChargeStructuralDamage;

    public SoundSpecifier? ChargeSound;

    [DataField]
    public TimeSpan BloodSwellShootPopupCooldown = TimeSpan.FromSeconds(1f);
    [DataField]
    public TimeSpan? BloodSwellShootNextPopupTime;

    [DataField]
    public EntityUid? BloodSwellShootLastGun;

    [DataField]
    public int PassiveHealBloodThreshold = 300;

    [DataField]
    public Dictionary<string, FixedPoint2> PassiveHealGroups = new()
    {
        { "Brute", FixedPoint2.New(3) },
        { "Burn", FixedPoint2.New(3) },
    };

    [DataField]
    public float OverwhelmingForcePrySpeedModifier = 10f;

    [DataField]
    public int OverwhelmingForceDoorPryBloodCost = 15;

    [DataField]
    public SoundSpecifier OverwhelmingForcePrySound = new SoundPathSpecifier("/Audio/Items/crowbar.ogg");
}
