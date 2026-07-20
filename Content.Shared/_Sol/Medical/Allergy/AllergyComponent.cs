using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// Mechanical allergies on a character.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AllergyComponent : Component
{
    /// <summary>
    /// Selected allergy prototype IDs. Prefer <see cref="Severities"/> for intensity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<AllergyPrototype>> Allergies = new();

    /// <summary>
    /// Per-allergy reaction intensity chosen in the lobby editor.
    /// Missing entries fall back to the prototype's <see cref="AllergyPrototype.DefaultSeverity"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<AllergyPrototype>, AllergySeverity> Severities = new();

    /// <summary>
    /// Allergies supplied by species metabolism rather than character preferences.
    /// Their reagent's existing metabolism effects provide the mechanical harm, so the
    /// allergy system reports the reaction without adding generic allergy damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AllergyPrototype>> InnateAllergies = new();

    /// <summary>
    /// Earliest time another standalone symptom popup may be shown (bloodstream path).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextSymptomPopup;

    /// <summary>
    /// Localized allergy name to append to the next food-taste popup for this eater.
    /// Cleared after the taste popup consumes it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? PendingTasteAllergyName;
}
