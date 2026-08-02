using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

//This entire file exists for the very niche case of renaming markings without breaking existing characters.
// As you can probably tell by reading it, it takes a lot from the mapping migration file.
namespace Content.Shared.Humanoid.Markings
{
    public sealed partial class MarkingManager
    {
        [Dependency] private IResourceManager _resourceManager = default!;

        private static readonly ResPath[] _migrationFiles =
        [
            new("/marking_migration.yml"),
            new("/_Starlight/marking_migration.yml"),
        ];

        private FrozenDictionary<string, string?> _migrations = new Dictionary<string, string?>().ToFrozenDictionary();

        private void CacheMigrations()
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
                if (!TryResolveMigration(oldId, out _))
                {
                    throw new InvalidOperationException(
                        $"Marking migration '{oldId}' does not resolve to a valid marking prototype.");
                }
            }
        }

        /// <summary>
        /// Resolves an old marking prototype ID through the marking migration files.
        /// </summary>
        public bool TryResolveMarkingId(string id, [NotNullWhen(true)] out string? resolvedId)
        {
            if (!TryResolveMigration(id, out resolvedId) || resolvedId == null)
            {
                resolvedId = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves a migration to a current prototype, or deletes it if you migrated it to null.
        /// </summary>
        private bool TryResolveMigration(string id, out string? resolvedId)
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

            if (!Markings.ContainsKey(id))
            {
                resolvedId = null;
                return false;
            }

            resolvedId = id;
            return true;
        }

        /// <summary>
        /// Resolves a marking and returns a copy containing its current prototype ID.
        /// </summary>
        public bool TryMigrateMarking(
            Marking marking,
            [NotNullWhen(true)] out Marking? migratedMarking,
            [NotNullWhen(true)] out MarkingPrototype? prototype)
        {
            migratedMarking = null;
            prototype = null;

            if (!TryResolveMarkingId(marking.MarkingId, out var markingId) ||
                !Markings.TryGetValue(markingId, out prototype))
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
}
