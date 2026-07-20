using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Knockback;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KnockbackByUserTagComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<TagPrototype>, KnockbackData> DoestContain = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class KnockbackData
{
    public KnockbackData() { }

    /// <summary>
    /// The distance / throw force of the weapon being fired.
    /// In units of distance.
    /// Shotguns should have a high knockback. Rifles should have medium to low. LMGs should have medium.
    /// </summary>
    [DataField]
    public float Knockback = 0;
    /// <summary>
    /// The amount of stamina damage taken per shot.
    /// </summary>
    [DataField]
    public float StaminaDamage = 0;
    /// <summary>
    /// Whether or not the ThrowSystem counts the entity as InAir.
    /// Keeping this value as false will make movement not feel painful for the player during automatic gunfire as BodyStatus.InAir prevents players from moving.
    /// True to enable ThrowingSystem.cs TryThrow() BodyStatus.InAir.
    /// </summary>
    [DataField]
    public bool DoFly = false;
    /// <summary>
    /// Is the knockback and stamina damage disabled by active magboots?
    /// </summary>
    [DataField]
    public bool IsDisabledByMagboots = false;
    /// <summary>
    /// Is the knockback and stamina damage reduced by active magboots?
    /// </summary>
    [DataField]
    public bool IsReducedByMagboots = false;
    /// <summary>
    /// How much is the knockback and stamina damage reduced by? (Multiplicative).
    /// 50 = 50% [0-100] (Value is not floored).
    /// A value of 50 would be calculated as -> Knockback * ( 1 - (50 / 100) ) -> Knockback * 0.5
    /// A value of 50 would be calculated as -> StaminaDamage * ( 1 - (50 / 100) ) -> StaminaDamage * 0.5
    /// </summary>
    [DataField]
    public float MagbootReductionMultiplier = 0;
}
