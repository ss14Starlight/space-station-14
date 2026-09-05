using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings
{
    public sealed partial class MarkingManager
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IResourceManager _resourceManager = default!; // Starlight

        private MarkingMigrationManager _migrationManager = default!; // Starlight
        private readonly List<MarkingPrototype> _index = new();
        public FrozenDictionary<MarkingCategories, FrozenDictionary<string, MarkingPrototype>> CategorizedMarkings = default!;
        public FrozenDictionary<string, MarkingPrototype> Markings = default!;

        public void Initialize()
        {
            _migrationManager = new MarkingMigrationManager(_resourceManager); // Starlight
            _prototypeManager.PrototypesReloaded += OnPrototypeReload;
            CachePrototypes();
            _migrationManager.CacheMigrations(Markings); // Starlight
        }

        private void CachePrototypes()
        {
            _index.Clear();
            var markingDict = new Dictionary<MarkingCategories, Dictionary<string, MarkingPrototype>>();

            foreach (var category in Enum.GetValues<MarkingCategories>())
            {
                markingDict.Add(category, new());
            }

            foreach (var prototype in _prototypeManager.EnumeratePrototypes<MarkingPrototype>())
            {
                _index.Add(prototype);
                markingDict[prototype.MarkingCategory].Add(prototype.ID, prototype);
            }

            Markings = _prototypeManager.EnumeratePrototypes<MarkingPrototype>().ToFrozenDictionary(x => x.ID);
            CategorizedMarkings = markingDict.ToFrozenDictionary(
                x => x.Key,
                x => x.Value.ToFrozenDictionary());
        }

        #region Starlight
        /// <summary>
        /// Resolves an old marking prototype ID through the marking migration files.
        /// </summary>
        public bool TryResolveMarkingId(string id, [NotNullWhen(true)] out string? resolvedId)
        {
            return _migrationManager.TryResolveMarkingId(id, Markings, out resolvedId);
        }

        /// <summary>
        /// Resolves a marking and returns a copy containing its current prototype ID.
        /// </summary>
        public bool TryMigrateMarking(
            Marking marking,
            [NotNullWhen(true)] out Marking? migratedMarking,
            [NotNullWhen(true)] out MarkingPrototype? prototype)
        {
            return _migrationManager.TryMigrateMarking(marking, Markings, out migratedMarking, out prototype);
        }
        #endregion

        public FrozenDictionary<string, MarkingPrototype> MarkingsByCategory(MarkingCategories category)
        {
            // all marking categories are guaranteed to have a dict entry
            return CategorizedMarkings[category];
        }

        /// <summary>
        ///     Markings by category and species.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpecies(MarkingCategories category,
            string species)
        {
            var speciesProto = _prototypeManager.Index<SpeciesPrototype>(species);
            var markingPoints = _prototypeManager.Index(speciesProto.MarkingPoints);
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if ((markingPoints.OnlyWhitelisted || markingPoints.Points[category].OnlyWhitelisted) && marking.SpeciesRestrictions == null)
                {
                    continue;
                }

                if (marking.SpeciesRestrictions != null && !marking.SpeciesRestrictions.Contains(species))
                {
                    continue;
                }
                res.Add(key, marking);
            }

            return res;
        }

        /// <summary>
        ///     Markings by category and sex.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="sex"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSex(MarkingCategories category,
            Sex sex)
        {
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if (marking.SexRestriction != null && marking.SexRestriction != sex)
                {
                    continue;
                }

                res.Add(key, marking);
            }

            return res;
        }

        /// <summary>
        ///     Markings by category, species and sex.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <param name="sex"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpeciesAndSex(MarkingCategories category,
            string species, Sex sex)
        {
            var speciesProto = _prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = _prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if (onlyWhitelisted && marking.SpeciesRestrictions == null)
                {
                    continue;
                }

                if (marking.SpeciesRestrictions != null && !marking.SpeciesRestrictions.Contains(species))
                {
                    continue;
                }

                if (marking.SexRestriction != null && marking.SexRestriction != sex)
                {
                    continue;
                }

                res.Add(key, marking);
            }

            return res;
        }

        public bool TryGetMarking(Marking marking, [NotNullWhen(true)] out MarkingPrototype? markingResult)
        {
            #region Starlight
            if (!TryResolveMarkingId(marking.MarkingId, out var markingId))
            {
                markingResult = null;
                return false;
            }

            return Markings.TryGetValue(markingId, out markingResult);
            #endregion
        }

        /// <summary>
        ///     Check if a marking is valid according to the category, species, and current data this marking has.
        /// </summary>
        /// <param name="marking"></param>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <param name="sex"></param>
        /// <returns></returns>
        public bool IsValidMarking(Marking marking, MarkingCategories category, string species, Sex sex)
        {
            if (!TryGetMarking(marking, out var proto))
            {
                return false;
            }

            if (proto.MarkingCategory != category ||
                proto.SpeciesRestrictions != null && !proto.SpeciesRestrictions.Contains(species) ||
                proto.SexRestriction != null && proto.SexRestriction != sex)
            {
                return false;
            }

            // Starlight edit - validate against exposed color slots, not raw sprite layers.
            if (marking.MarkingColors.Count != proto.ColorSlotCount)
            {
                return false;
            }

            return true;
        }

        private void OnPrototypeReload(PrototypesReloadedEventArgs args)
        {
            #region Starlight
            if (!args.WasModified<MarkingPrototype>())
                return;

            CachePrototypes();
            _migrationManager.CacheMigrations(Markings); // Starlight
            #endregion
        }

        public bool CanBeApplied(string species, Sex sex, Marking marking, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);

            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;

            if (!TryGetMarking(marking, out var prototype))
            {
                return false;
            }

            if (onlyWhitelisted && prototype.SpeciesRestrictions == null)
            {
                return false;
            }

            if (prototype.SpeciesRestrictions != null
                && !prototype.SpeciesRestrictions.Contains(species))
            {
                return false;
            }

            if (prototype.SexRestriction != null && prototype.SexRestriction != sex)
            {
                return false;
            }

            return true;
        }

        public bool CanBeApplied(string species, Sex sex, MarkingPrototype prototype, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);

            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;

            if (onlyWhitelisted && prototype.SpeciesRestrictions == null)
            {
                return false;
            }

            if (prototype.SpeciesRestrictions != null &&
                !prototype.SpeciesRestrictions.Contains(species))
            {
                return false;
            }

            if (prototype.SexRestriction != null && prototype.SexRestriction != sex)
            {
                return false;
            }

            return true;
        }

        public bool MustMatchSkin(string species, HumanoidVisualLayers layer, out float alpha, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);
            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            if (
                !prototypeManager.Resolve(speciesProto.SpriteSet, out var baseSprites) ||
                !baseSprites.Sprites.TryGetValue(layer, out var spriteName) ||
                !prototypeManager.Resolve(spriteName, out HumanoidSpeciesSpriteLayer? sprite) ||
                sprite == null ||
                !sprite.MarkingsMatchSkin
            )
            {
                alpha = 1f;
                return false;
            }

            alpha = sprite.LayerAlpha;
            return sprite.MarkingsMatchSkin;
        }
    }
}
