using System.Linq;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Utility;

/**
 * Extension methods for <see cref="FormattedMessage"/>s centered around sanitization.
 */
public static class FormattedMessageSanitizer
{
    /// <summary>
    /// Simple tags that are safe to use for item labels. No interactive tags, size changes (head tag) or images.
    /// </summary>
    public static string[] ItemLabelTags =
        ["color", "bold", "bolditalic", "italic", "mono", "dots"];

    /// <summary>
    /// Tags that are safe to use for paper labels, like invoices that can be attached to crates.
    /// </summary>
    public static string[] PaperLabelTags =
    [
        "color", "bold", "bolditalic", "italic", "mono", "icon", "scramble", "font", "head", "bullet", "dots", "dothead"
    ];

    /// <param name="message">The message to sanitize</param>
    extension(FormattedMessage message)
    {
        /// <summary>
        /// Sanitize the given message using a whitelist, allowing only explicitly permitted tags and/or raw text.
        /// </summary>
        /// <param name="permittedTagTypes">The tag names that are permitted</param>
        /// <param name="permitText">If raw text is permitted</param>
        /// <returns>A new <see cref="FormattedMessage"/> without the nodes that failed to pass the filter.</returns>
        public FormattedMessage SanitizeWhitelist(string[] permittedTagTypes, bool permitText = true) =>
            message.SanitizeFlat(permittedTagTypes, permitText, inclusive: true);

        /// <summary>
        /// Sanitize the given message using a blacklist, removing only explicitly disallowed tags and/or raw text.
        /// </summary>
        /// <param name="disallowedTagTypes">The tag names that are disallowed</param>
        /// <param name="permitText">If raw text is permitted</param>
        /// <returns>A new <see cref="FormattedMessage"/> without the nodes that failed to pass the filter.</returns>
        public FormattedMessage SanitizeBlacklist(string[] disallowedTagTypes, bool permitText = true) =>
            message.SanitizeFlat(disallowedTagTypes, permitText, inclusive: false);

        /// <summary>
        /// Sanitize the given message, removing anything that is not permitted. This does not do anything intelligent
        /// with a tag hierarchy, it merely removes nodes that do not match the filter, hence the "flat" in the name.
        /// </summary>
        /// <param name="tagTypes">The tag names that are targeted, see "inclusive"</param>
        /// <param name="permitText">If raw text is permitted</param>
        /// <param name="inclusive">Whether the tags in tagTypes are to be included or excluded.</param>
        /// <returns>A new <see cref="FormattedMessage"/> without the nodes that failed to pass the filter.</returns>
        private FormattedMessage SanitizeFlat(string[] tagTypes, bool permitText, bool inclusive = false)
        {
            FormattedMessage sanitized = new();
            foreach (var node in message.Nodes)
            {
                // If text tag and it's permitted.
                if (node.Name == null && permitText)
                {
                    sanitized.PushTag(node);
                    continue;
                }

                // If non-text tag and it's whitelisted.
                if (node.Name != null && tagTypes.Contains(node.Name) == inclusive)
                    sanitized.PushTag(node);

                // The rest is removed.
            }

            return sanitized;
        }

        /// <summary>
        /// Removes leading tag blocks that match the given tag types. This method is hierarchical, meaning it will
        /// remove entire blocks -- including their content -- if they match the criteria.
        /// </summary>
        /// <param name="tagTypes">The types of leading tags to remove</param>
        /// <returns>A new <see cref="FormattedMessage"/> without the nodes that failed to pass the filter.</returns>
        public FormattedMessage RemoveLeading(string[] tagTypes)
        {
            FormattedMessage sanitized = new();
            var depth = 0;
            var done = false;
            foreach (var node in message.Nodes)
            {
                // If this is the target tag, update our depth and continue.
                if (!done && node.Name != null && tagTypes.Contains(node.Name))
                {
                    depth = Math.Max(0, depth + (node.Closing ? -1 : 1));

                    // If depth is zero after updating, we consider it the end of the "leading" sequence.
                    if (depth == 0)
                        done = true;

                    continue;
                }

                // If we're inside a deleted block, the content gets deleted too.
                if (depth > 0)
                    continue;

                // If we reach here, it's not a deleted block, and it's not a leading tag,
                // so we're done for sure too.
                done = true;
                sanitized.PushTag(node);
            }

            return sanitized;
        }
    }
}
