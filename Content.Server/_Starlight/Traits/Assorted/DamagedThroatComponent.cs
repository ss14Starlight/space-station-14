using Content.Shared.Damage.Prototypes;
using Content.Shared._Starlight.Language;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Traits.Assorted;

/// <summary>
///     When applied to a player entity, causes them to take damage and cough when speaking normally.
///     Whispering bypasses this effect.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DamagedThroatComponent : Component
{
    /// <summary>
    ///     The type of damage to apply when speaking normally.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Blunt";

    /// <summary>
    ///     Languages that should not trigger damage (e.g., sign language).
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>> ExcludedLanguages = new()
    {
        "Sign"
    };

    /// <summary>
    ///     The base damage to apply when speaking normally (starts at this value).
    /// </summary>
    [DataField]
    public float BaseDamage = 2f;

    /// <summary>
    ///     How much damage increases each time you speak.
    /// </summary>
    [DataField]
    public float DamageIncrement = 2f;

    /// <summary>
    ///     Maximum damage cap.
    /// </summary>
    [DataField]
    public float MaxDamage = 10f;

    /// <summary>
    ///     Current damage level (tracks escalation).
    /// </summary>
    [DataField]
    public float CurrentDamage = 2f;

    /// <summary>
    ///     The chance (0.0 to 1.0) to cough when speaking normally.
    /// </summary>
    [DataField]
    public float CoughChance = 0.6f;

    /// <summary>
    ///     The minimum time between damage applications.
    /// </summary>
    [DataField]
    [AutoPausedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Time without speaking normally before damage resets to base.
    /// </summary>
    [DataField]
    [AutoPausedField]
    public TimeSpan ResetCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     The last time the entity spoke normally (not whisper).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastSpeakTime;
}
