using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.NanoChat;

/// <summary>
/// Centralized cache for NanoChat emote definitions.
/// </summary>
public static class NanoChatEmoteCache
{
    private static Dictionary<string, EmoteData>? _emoteCache;
    private static Dictionary<string, List<EmoteData>>? _categoryCache;
    private static List<string>? _allCategories;

    /// <summary>
    /// Data structure for cached emote information.
    /// </summary>
    public sealed class EmoteData
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public List<string> SearchTags { get; init; } = [];
        public SpriteSpecifier Sprite { get; init; } = default!;
        public int Priority { get; init; }
        public string SearchString { get; init; } = string.Empty;
    }

    /// <summary>
    /// Gets all emotes mapped by their ID.
    /// </summary>
    public static IReadOnlyDictionary<string, EmoteData> AllEmotes
    {
        get
        {
            EnsureCacheLoaded();
            return _emoteCache!;
        }
    }

    /// <summary>
    /// Gets emotes organized by category.
    /// </summary>
    public static IReadOnlyDictionary<string, List<EmoteData>> EmotesByCategory
    {
        get
        {
            EnsureCacheLoaded();
            return _categoryCache!;
        }
    }

    /// <summary>
    /// Gets all available category names.
    /// </summary>
    public static IReadOnlyList<string> Categories
    {
        get
        {
            EnsureCacheLoaded();
            return _allCategories!;
        }
    }

    /// <summary>
    /// Gets sprite specifier for an emote by ID.
    /// </summary>
    public static SpriteSpecifier? GetEmoteSprite(string emoteId)
    {
        EnsureCacheLoaded();
        return _emoteCache!.TryGetValue(emoteId, out var data) ? data.Sprite : null;
    }

    /// <summary>
    /// Checks if an emote exists.
    /// </summary>
    public static bool EmoteExists(string emoteId)
    {
        EnsureCacheLoaded();
        return _emoteCache!.ContainsKey(emoteId);
    }

    /// <summary>
    /// Searches emotes by query string.
    /// Searches ID, display name, and tags.
    /// </summary>
    public static List<EmoteData> SearchEmotes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return AllEmotes.Values.ToList();

        EnsureCacheLoaded();
        
        var lowerQuery = query.ToLowerInvariant();
        var results = new List<EmoteData>();

        // Exact ID matches first
        if (_emoteCache!.TryGetValue(lowerQuery, out var exactMatch))
        {
            results.Add(exactMatch);
        }

        foreach (var emote in _emoteCache.Values)
        {
            if (emote.Id == lowerQuery)
                continue; // Already added

            if (emote.SearchString.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(emote);
            }
        }

        return results.OrderBy(e => e.Category).ThenBy(e => e.Priority).ThenBy(e => e.Id).ToList();
    }

    /// <summary>
    /// Gets emotes in a specific category.
    /// </summary>
    public static List<EmoteData> GetEmotesInCategory(string category)
    {
        EnsureCacheLoaded();
        return _categoryCache!.TryGetValue(category, out var emotes) 
            ? new List<EmoteData>(emotes) 
            : new List<EmoteData>();
    }

    /// <summary>
    /// Force reload of the emote cache.
    /// Call this when prototypes are reloaded.
    /// </summary>
    public static void InvalidateCache()
    {
        _emoteCache = null;
        _categoryCache = null;
        _allCategories = null;
    }

    private static void EnsureCacheLoaded()
    {
        if (_emoteCache != null)
            return;

        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _emoteCache = new Dictionary<string, EmoteData>();
        _categoryCache = new Dictionary<string, List<EmoteData>>();
        var categories = new HashSet<string>();

        foreach (var proto in prototypeManager.EnumeratePrototypes<NanoChatEmotePrototype>())
        {
            var displayName = proto.DisplayName ?? proto.ID;
            var searchTags = new List<string>(proto.SearchTags);
            
            // Build comprehensive search string
            var searchParts = new List<string> { proto.ID, displayName, proto.Category };
            searchParts.AddRange(searchTags);
            var searchString = string.Join(" ", searchParts).ToLowerInvariant();

            var emoteData = new EmoteData
            {
                Id = proto.ID,
                DisplayName = displayName,
                Category = proto.Category,
                SearchTags = searchTags,
                Sprite = proto.Sprite,
                Priority = proto.Priority,
                SearchString = searchString
            };

            _emoteCache[proto.ID] = emoteData;
            categories.Add(proto.Category);

            if (!_categoryCache.ContainsKey(proto.Category))
                _categoryCache[proto.Category] = new List<EmoteData>();
            
            _categoryCache[proto.Category].Add(emoteData);
        }

        // Sort emotes within each category
        foreach (var category in _categoryCache.Keys.ToList())
        {
            _categoryCache[category] = _categoryCache[category]
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.Id)
                .ToList();
        }

        _allCategories = categories.OrderBy(c => c).ToList();
    }
}
