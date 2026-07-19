using System.Linq;
using Content.Shared._Sol.Medical.Allergy;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    /// <summary>
    /// Structured Sol mechanical allergies selected in the lobby allergy editor.
    /// </summary>
    [DataField("solAllergies")]
    public List<CharacterAllergyPreference> SolAllergies { get; private set; } = [];

    public HumanoidCharacterProfile WithSolAllergies(IEnumerable<CharacterAllergyPreference> allergies)
    {
        var copy = allergies.Select(a => new CharacterAllergyPreference(a)).ToList();
        return new HumanoidCharacterProfile(this) { SolAllergies = copy };
    }

    public static string FormatSeverity(AllergySeverity severity)
    {
        return severity switch
        {
            AllergySeverity.Mild => Loc.GetString("cd-allergies-editor-intensity-mild"),
            AllergySeverity.Moderate => Loc.GetString("cd-allergies-editor-intensity-moderate"),
            AllergySeverity.Severe => Loc.GetString("cd-allergies-editor-intensity-severe"),
            AllergySeverity.Anaphylaxis => Loc.GetString("cd-allergies-editor-intensity-extreme"),
            _ => severity.ToString(),
        };
    }

    public static AllergySeverity ParseSeverity(string? text, AllergySeverity fallback = AllergySeverity.Mild)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        if (Enum.TryParse(text, ignoreCase: true, out AllergySeverity parsed) &&
            parsed != AllergySeverity.Anaphylaxis)
            return parsed;

        // Lobby UI uses "Extreme" for anaphylactic intensity.
        if (text.Equals("Extreme", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("Anaphylaxis", StringComparison.OrdinalIgnoreCase))
            return AllergySeverity.Anaphylaxis;

        return fallback;
    }

    public void EnsureValidSolAllergies(IPrototypeManager prototypes)
    {
        SolAllergies ??= [];
        var cleaned = new List<CharacterAllergyPreference>();
        var seen = new HashSet<ProtoId<AllergyPrototype>>();

        foreach (var entry in SolAllergies)
        {
            if (!prototypes.HasIndex(entry.AllergyId) || !seen.Add(entry.AllergyId))
                continue;

            var severity = entry.Severity;
            if (!Enum.IsDefined(severity))
                severity = AllergySeverity.Mild;

            cleaned.Add(new CharacterAllergyPreference(entry.AllergyId, severity));
        }

        SolAllergies = cleaned;
    }
}
