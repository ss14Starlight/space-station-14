using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// Ongoing allergic reaction. Severe+ blocks asphyxiation healing, clamps breathing
/// saturation (airway closing), and ticks airloss damage until <see cref="EndsAt"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ActiveAllergyReactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AllergyPrototype> AllergyId;

    [DataField, AutoNetworkedField]
    public AllergySeverity Severity = AllergySeverity.Severe;

    /// <summary>
    /// When symptoms / speech struggle begin. Set in the future for ingested allergens
    /// so onset lags ~1–2 seconds behind the taste warning.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan DamageStartsAt;

    /// <summary>
    /// When asphyxiation damage and hard airway clamping begin.
    /// Delayed slightly after <see cref="DamageStartsAt"/> so choking is felt before airloss ticks.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan AirlossStartsAt;

    /// <summary>
    /// When the reaction wears off. Remaining time may be extended by further exposure
    /// up to a max remaining duration from now (not from reaction start).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>
    /// Multiplier on per-tick damage from how much allergen has been consumed this bout.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity = 1f;

    /// <summary>
    /// Whether speech was muted for this reaction (cleared on shutdown).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AppliedMute;

    /// <summary>
    /// Whether stuttering speech struggle was applied for this reaction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AppliedStutter;

    /// <summary>
    /// Whether the delayed-onset symptom popup has already been shown for this bout.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OnsetPopupShown;
}
