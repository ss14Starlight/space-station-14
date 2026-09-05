using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

//This entire file exists for the very niche case of renaming markings without breaking existing characters.
// As you can probably tell by reading it, it takes a lot from the mapping migration file.
namespace Content.Shared._Starlight.Humanoid.Markings;

/// <summary>
/// Loads marking prototype migrations and resolves markings saved with obsolete IDs.
/// </summary>
internal sealed class MarkingMigrationManager
{
    private readonly IResourceManager _resourceManager;

    private static readonly ResPath[] _migrationFiles =
    [
        new("/marking_migration.yml"),
        new("/_Starlight/marking_migration.yml"),
    ];

    private FrozenDictionary<string, string?> _migrations =
        new Dictionary<string, string?>().ToFrozenDictionary();

    internal MarkingMigrationManager(IResourceManager resourceManager) => _resourceManager = resourceManager;

    internal void CacheMigrations(IReadOnlyDictionary<string, MarkingPrototype> markings)
    {
        var migrations = new Dictionary<string, string?>();

        foreach (var file in _migrationFiles)
        {
            if (!_resourceManager.TryContentFileRead(file, out var stream))
                continue;

            using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
            var document = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();

            if (document?.Root is not MappingDataNode mappings)
                continue;

            foreach (var (oldId, node) in mappings)
            {
                if (node is not ValueDataNode valueNode)
                    continue;

                var newId = string.IsNullOrWhiteSpace(valueNode.Value) || valueNode.Value == "null"
                    ? null
                    : valueNode.Value;

                if (!migrations.TryAdd(oldId, newId))
                    throw new InvalidOperationException($"Duplicate marking migration for '{oldId}'.");
            }
        }

        _migrations = migrations.ToFrozenDictionary();

        // Validate after loading every file so migrations may be chained.
        foreach (var oldId in _migrations.Keys)
        {
            if (!TryResolveMigration(oldId, markings, out _))
            {
                throw new InvalidOperationException($"Marking migration '{oldId}' does not resolve to a valid marking prototype.");
            }
        }
    }

    internal bool TryResolveMarkingId(
        string id,
        IReadOnlyDictionary<string, MarkingPrototype> markings,
        [NotNullWhen(true)] out string? resolvedId)
    {
        if (!TryResolveMigration(id, markings, out resolvedId) || resolvedId == null)
        {
            resolvedId = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves a migration to a current prototype, or deletes it if it was migrated to null.
    /// </summary>
    private bool TryResolveMigration(
        string id,
        IReadOnlyDictionary<string, MarkingPrototype> markings,
        out string? resolvedId)
    {
        HashSet<string>? visited = null;

        while (_migrations.TryGetValue(id, out var replacement))
        {
            if (replacement == null)
            {
                resolvedId = null;
                return true;
            }

            visited ??= [];
            if (!visited.Add(id))
                throw new InvalidOperationException($"Circular marking migration involving '{id}'.");

            id = replacement;
        }

        if (!markings.ContainsKey(id))
        {
            resolvedId = null;
            return false;
        }

        resolvedId = id;
        return true;
    }

    internal bool TryMigrateMarking(
        Marking marking,
        IReadOnlyDictionary<string, MarkingPrototype> markings,
        [NotNullWhen(true)] out Marking? migratedMarking,
        [NotNullWhen(true)] out MarkingPrototype? prototype)
    {
        migratedMarking = null;
        prototype = null;

        if (!TryResolveMarkingId(marking.MarkingId, markings, out var markingId) ||
            !markings.TryGetValue(markingId, out prototype))
        {
            return false;
        }

        if (markingId == marking.MarkingId)
        {
            migratedMarking = marking;
            return true;
        }

        migratedMarking = new Marking(markingId, marking.MarkingColors, marking.IsGlowing)
        {
            Forced = marking.Forced,
            Visible = marking.Visible,
        };

        return true;
    }
}
