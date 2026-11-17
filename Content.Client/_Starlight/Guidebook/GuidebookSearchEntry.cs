using System.Linq;
using System.Text;
using Content.Shared.Guidebook;

namespace Content.Client._Starlight.Guidebook;

/// <summary>
///     Cached, localized search terms for a guidebook entry.
/// </summary>
internal sealed class GuidebookSearchEntry
{
    /// <summary>
    ///     Normalized title and keyword phrases cached for matching.
    /// </summary>
    private readonly string[] _terms;

    /// <summary>
    ///     Caches the localized title and alternative search phrases without case or separators.
    /// </summary>
    public GuidebookSearchEntry(string name, IEnumerable<string> keywords) =>
        _terms = keywords.Prepend(name).Select(Normalize).Where(term => term.Length > 0).ToArray();

    /// <summary>
    ///     Combines an entry's localized title, shared keyword catalog, and additional prototype keywords.
    /// </summary>
    public static GuidebookSearchEntry FromEntry(GuideEntry entry, ILocalizationManager localizationManager)
    {
        var keywords = entry.SearchKeywords.Select(keyword => localizationManager.GetString(keyword));
        if (localizationManager.TryGetString(
                $"guidebook-search-keywords-{entry.Id.ToLowerInvariant()}",
                out var catalogKeywords))
        {
            keywords = keywords.Concat(catalogKeywords.Split(';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return new GuidebookSearchEntry(localizationManager.GetString(entry.Name), keywords);
    }

    /// <summary>
    ///     Matches a phrase or all query words against the title and aliases, ignoring case and separators.
    /// </summary>
    public bool Matches(string query)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0)
            return true;

        if (_terms.Any(term => term.Contains(normalized, StringComparison.Ordinal)))
            return true;

        // Also accept words in a different order, or words from both the title and its aliases.
        return GetWords(query).All(word => _terms.Any(term => term.Contains(word, StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Extracts distinct lowercase words from a query, treating punctuation and whitespace as separators.
    /// </summary>
    private static IEnumerable<string> GetWords(string query)
    {
        var separated = new string(query.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray());
        return separated.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct();
    }

    /// <summary>
    ///     Keeps only lowercase letters and digits so joined words and spaced phrases can match.
    /// </summary>
    private static string Normalize(string text)
    {
        var normalized = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
                normalized.Append(char.ToLowerInvariant(c));
        }

        return normalized.ToString();
    }
}
