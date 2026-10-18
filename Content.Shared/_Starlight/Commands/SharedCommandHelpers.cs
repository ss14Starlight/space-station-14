using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Shared._Starlight.Commands;

public static class SharedCommandHelpers
{
    // Literally just sick and tired of checking for this manually ngl.
    public static bool NoSession(IInvocationContext ctx)
    {
        if (ctx.Session is not null) return false;
        CommandMarkup.Error(ctx, "Cannot be called from server.");
        return true;
    }

    /// <summary>
    /// Get the player name associated with the context's session, or "Server" if null.
    /// </summary>
    public static string PlayerNameOrServer(ICommonSession? session) =>
        session is null ? "Server" : session.Name;

    /// <summary>
    /// Get the player name associated with the context's session, or "Server" if null.
    /// </summary>
    public static string PlayerNameOrServer(IInvocationContext ctx) =>
        PlayerNameOrServer(ctx.Session);

    /// <summary>
    /// Gets the text of the commands whose output gets piped into the command starting at the given index.
    /// That is, everything since the start of the enclosing block or the last command terminator.
    /// </summary>
    public static string GetPipedExpression(string input, int commandStart)
    {
        var starts = new Stack<int>();
        starts.Push(0);
        var inString = false;

        for (var i = 0; i < commandStart; i++)
        {
            var c = input[i];
            if (inString)
            {
                switch (c)
                {
                    case '\\':
                        i++;
                        break;
                    case '"':
                        inString = false;
                        break;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    starts.Push(i + 1);
                    break;
                case '}' when starts.Count > 1:
                    starts.Pop();
                    break;
                case ';' or '\n':
                    starts.Pop();
                    starts.Push(i + 1);
                    break;
            }
        }

        return input[starts.Peek()..commandStart].Trim();
    }
}
