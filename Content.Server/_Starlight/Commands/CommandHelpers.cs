using System.Linq;
using System.Reflection;
using Content.Shared._Starlight.Commands;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
// ReSharper disable UseCollectionExpression

namespace Content.Server._Starlight.Commands;

public static class CommandHelpers
{
    // List of commands that are easy to reparse for entity uids. No I am not doing other types. I am lazy.
    private static readonly HashSet<string> _safePipedCommands = new()
    {
        "ent",
        "entities",
        "marked",
        "self",
        "nearby",
        "with",
        "named",
        "prototyped",
        "paused",
        "first",
        "take",
    };

    // voodoo black magic go
    private static readonly FieldInfo? _varParserCtxField =
        typeof(InvocationCtxVarParser).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic);

    public static object? EvaluatePipedInput(ParserContext ctx)
    {
        if (GetInvocationContext(ctx) is not { } invCtx) return null;

        // input is piped into the enclosing block, which means cant eval properly
        var expr = SharedCommandHelpers.GetPipedExpression(ctx.Input, ctx.Bundle.NameStart);
        if (string.IsNullOrWhiteSpace(expr)) return null;

        // blocks are evil and cant be evaluated like this
        if (expr.Contains('{')) return null;

        var parser = new ParserContext(expr, ctx.Toolshed, invCtx);
        if (!CommandRun.TryParse(parser, null, null, out var run) || !parser.OutOfInput) return null;

        if (run.Commands.Any(cmd => !_safePipedCommands.Contains(cmd.Item1.Command.Name))) return null;
        if (invCtx.HasErrors) return null;

        var result = run.Invoke(null, invCtx, reportErrors: false);
        invCtx.ClearErrors();
        return result;
    }

    public static object? ReadVar(ParserContext ctx, string name)
    {
        if (GetInvocationContext(ctx) is { } invCtx)
            return invCtx.ReadVar(name);

        // Fallback in case we can't get at the invocation context.
        return name == "self" ? ctx.Session?.AttachedEntity : null;
    }

    public static IInvocationContext? GetInvocationContext(ParserContext ctx)
    {
        var parser = ctx.VariableParser;
        while (parser is LocalVarParser local)
        {
            parser = local.Inner;
        }

        return parser is InvocationCtxVarParser ? _varParserCtxField?.GetValue(parser) as IInvocationContext : null;
    }
}
