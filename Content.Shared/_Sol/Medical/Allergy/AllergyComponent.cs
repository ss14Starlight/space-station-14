using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// Mechanical allergies on a character.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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
}
