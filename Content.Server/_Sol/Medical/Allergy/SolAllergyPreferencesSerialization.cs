using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Content.Shared._CD.Records;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.Preferences;

namespace Content.Server._Sol.Medical.Allergy;

/// <summary>
/// Persists structured lobby allergies inside the existing CD character-records JSON blob
/// so severity survives database round-trips without a schema migration.
/// </summary>
public static class SolAllergyPreferencesSerialization
{
    public const string JsonKey = "SolAllergies";

    private sealed class SolAllergyDbEntry
    {
        public string AllergyId { get; set; } = string.Empty;
        public string Severity { get; set; } = nameof(AllergySeverity.Mild);
    }

    public static JsonDocument EmbedInCharacterRecords(
        PlayerProvidedCharacterRecords records,
        IReadOnlyList<CharacterAllergyPreference> allergies)
    {
        var node = JsonSerializer.SerializeToNode(records)?.AsObject()
                   ?? new JsonObject();

        var entries = allergies.Select(a => new SolAllergyDbEntry
        {
            AllergyId = a.AllergyId.Id,
            Severity = a.Severity.ToString(),
        }).ToList();

        node[JsonKey] = JsonSerializer.SerializeToNode(entries);
        return JsonSerializer.SerializeToDocument(node);
    }

    public static List<CharacterAllergyPreference> ReadFromCharacterRecords(JsonDocument? json)
    {
        var result = new List<CharacterAllergyPreference>();
        if (json is null)
            return result;

        if (!json.RootElement.TryGetProperty(JsonKey, out var array) ||
            array.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in array.EnumerateArray())
        {
            if (!element.TryGetProperty(nameof(SolAllergyDbEntry.AllergyId), out var idProp) ||
                idProp.ValueKind != JsonValueKind.String)
                continue;

            var id = idProp.GetString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var severity = AllergySeverity.Mild;
            if (element.TryGetProperty(nameof(SolAllergyDbEntry.Severity), out var sevProp) &&
                sevProp.ValueKind == JsonValueKind.String)
            {
                severity = HumanoidCharacterProfile.ParseSeverity(sevProp.GetString(), AllergySeverity.Mild);
            }

            result.Add(new CharacterAllergyPreference(id, severity));
        }

        return result;
    }
}
