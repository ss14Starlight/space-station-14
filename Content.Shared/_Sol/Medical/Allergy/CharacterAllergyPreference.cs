using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// A character's selected allergy and preferred reaction intensity from the lobby editor.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class CharacterAllergyPreference
{
    [DataField(required: true)]
    public ProtoId<AllergyPrototype> AllergyId = default!;

    [DataField]
    public AllergySeverity Severity = AllergySeverity.Mild;

    public CharacterAllergyPreference()
    {
    }

    public CharacterAllergyPreference(ProtoId<AllergyPrototype> allergyId, AllergySeverity severity)
    {
        AllergyId = allergyId;
        Severity = severity;
    }

    public CharacterAllergyPreference(CharacterAllergyPreference other)
    {
        AllergyId = other.AllergyId;
        Severity = other.Severity;
    }

    public bool MemberwiseEquals(CharacterAllergyPreference other)
    {
        return AllergyId == other.AllergyId && Severity == other.Severity;
    }
}
