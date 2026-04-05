using System.Linq;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Utility;

/**
 * Extension methods for <see cref="FormattedMessage"/>s centered around sanitization.
 */
public static class FormattedMessageSanitizer
{
    /**
     * Simple tags that are safe to use for item labels. No interactive tags, size changes (head tag) or images.
     */
    public static string[] ItemLabelTags = new[] { "color", "bold", "bolditalic", "italic", "mono" };

    /// <summary>
    /// Sanitize the given message using a whitelist, allowing only explicitly permitted tags and/or raw text.
    /// </summary>
    /// <param name="message">The message to sanitize</param>
    /// <param name="permittedTagTypes">The tag names that are permitted</param>
    /// <param name="permitText">If raw text is permitted</param>
    /// <returns>A new <see cref="FormattedMessage"/> without the nodes that failed to pass the filter.</returns>
    public static FormattedMessage SanitizeWhitelist(this FormattedMessage message, string[] permittedTagTypes,
        bool permitText = true)
    {
        FormattedMessage sanitized = new();
        foreach (var node in message.Nodes)
        {
            // If text tag and it's permitted
            if (node.Name == null && permitText)
            {
                sanitized.PushTag(node);
                continue;
            }

            // If non-text tag and it's whitelisted
            if (node.Name != null && permittedTagTypes.Contains(node.Name))
                sanitized.PushTag(node);

            // The rest is removed.
        }

        return sanitized;
    }
}
