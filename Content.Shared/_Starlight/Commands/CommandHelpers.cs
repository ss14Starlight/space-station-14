using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Shared._Starlight.Commands;

public static class CommandHelpers
{
    // Literally just sick and tired of checking for this manually ngl.
    public static bool NoSession(IInvocationContext ctx)
    {
        if (ctx.Session is not null) return false;
        CommandMarkup.Error(ctx, "Cannot be called from server.");
        return true;
    }

    /// Get the player name associated with the context's session, or "Server" if null.
    public static string PlayerNameOrServer(ICommonSession? session) =>
        session is null ? "Server" : session.Name;

    /// <summary>
    /// Get the player name associated with the context's session, or "Server" if null.
    /// </summary>
    public static string PlayerNameOrServer(IInvocationContext ctx) =>
        PlayerNameOrServer(ctx.Session);
}
