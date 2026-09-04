using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Commands;

/// Helper class to streamline doing color markup when outputting to console
public static class CommandMarkup
{
    /// <summary>
    /// Color the input message red for error emphasis.
    /// </summary>
    /// <param name="ctx">IInvocationContext instance.</param>
    /// <param name="message">The message you want to color.</param>
    public static void Error(IInvocationContext ctx, string message) =>
        ctx.WriteMarkup($"[color=red]{message}[/color]");

    /// <summary>
    /// Color the input message gold for warning emphasis.
    /// </summary>
    /// <param name="ctx">IInvocationContext instance.</param>
    /// <param name="message">The message you want to color.</param>
    public static void Warn(IInvocationContext ctx, string message) =>
        ctx.WriteMarkup($"[color=gold]{message}[/color]");

    /// <summary>
    /// Highlight a section of a message. Default color is magenta.
    /// </summary>
    /// <param name="ctx">IInvocationContext instance.</param>
    /// <param name="text">Text that you want to highlight.</param>
    /// <param name="color">Color you want to highlight with.</param>
    /// <param name="spaced">Add spaces around the text or not.</param>
    /// <returns>Formatted string with markup inserted to highlight the input text.</returns>
    public static string Highlight(IInvocationContext ctx, string text, Color? color = null, bool spaced = false) =>
        $"{(spaced ? " " : "")}[color={color?.ToHex() ?? Color.Magenta.ToHex()}]{text}[/color]{(spaced ? " " : "")}";
}
