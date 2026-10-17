using System.Linq;
using System.Reflection;
using Content.Client.RichText;
using Content.Client.UserInterface.RichText;
using Robust.Client.UserInterface.RichText;
#pragma warning disable CS0618 // Type or member is obsolete
// ReSharper disable UseCollectionExpression

namespace Content.Client._Starlight.RichText;

public sealed class SLUserFormattableTags
{
    private static Type[]? _allTags;

    /// <summary>
    /// Returns all existing markup tags, both from engine and content.
    /// </summary>
    public static Type[] AllTags() =>
        _allTags ??= new[] { typeof(IMarkupTagHandler).Assembly, typeof(SLUserFormattableTags).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false } &&
                (typeof(IMarkupTagHandler).IsAssignableFrom(t) || typeof(IMarkupTag).IsAssignableFrom(t)))
            .ToArray();

    // Notably, missing cmdlink.
    public static Type[] ExtendedMarkup =
    [
        ..UserFormattableTags.BaseAllowedTags,
        typeof(ScrambleTag),
        typeof(IconTag),
        typeof(FontTag)
    ];
}
