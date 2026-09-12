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
}
