using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Commands;

/// Helper class to streamline doing color markup when outputting to console
public static class CommandMarkup
{
    public static void Error(IInvocationContext ctx, string message) =>
        ctx.WriteMarkup($"[color=red]{message}[/color]");

    public static void Warn(IInvocationContext ctx, string message) =>
        ctx.WriteMarkup($"[color=gold]{message}[/color]");

    /// Highlight section of text
    public static string Highlight(IInvocationContext ctx, string text, Color? color = null, bool spaced = false) =>
        $"{(spaced ? " " : "")}[color={color?.ToHex() ?? Color.Magenta.ToHex()}]{text}[/color]{(spaced ? " " : "")}";
}
