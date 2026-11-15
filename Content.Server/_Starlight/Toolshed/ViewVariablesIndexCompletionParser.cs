using System.Collections;
using System.Linq;
using Content.Server._Starlight.Commands;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class ViewVariablesIndexCompletionParser : CustomCompletionParser<int>
{
    [Dependency] private IEntityManager _ent = null!;
    [Dependency] private IViewVariablesManager _vvm = null!;

    private const int MaxHintLength = 64;
    private const int MaxOpts = 128;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        ctx.ConsumeWhitespace();

        if (ResolveCollection(ctx) is not { } collection)
            return CompletionResult.FromHint("Not a valid array or dictionary");

        var opts = collection.Cast<object?>()
            .Take(MaxOpts).Select((item, i) => new CompletionOption(i.ToString(), Describe(item)));

        return CompletionResult.FromHintOptions(opts, GetArgHint(arg));
    }

    private IEnumerable? ResolveCollection(ParserContext ctx)
    {
        if (GetPath(ctx) is not { } path)
            return null;

        path = path.TrimStart('/');

        if (ctx.Bundle.PipedType is null)
            return ResolveCollection(path);

        var uid = CommandHelpers.EvaluatePipedInput(ctx) switch
        {
            EntityUid u => u,
            IEnumerable<EntityUid> uids => uids.FirstOrDefault(),
            _ => EntityUid.Invalid,
        };

        return uid.IsValid() ? ResolveCollection($"entity/{uid}/{path}") : null;
    }

    private static string? GetPath(ParserContext ctx) =>
        ctx.Bundle.Arguments?.GetValueOrDefault("path") switch
        {
            string path => path,
            BlockRef<string> => null,
            ValueRef<string> valueRef => CommandHelpers.GetInvocationContext(ctx) is { } invCtx
                ? valueRef.Evaluate(invCtx)
                : null,
            _ => null
        };

    private IEnumerable? ResolveCollection(string path)
        => _vvm.ResolvePath(path)?.Get() is IEnumerable enumerable and not string ? enumerable : null;

    private string Describe(object? item)
    {
        var text = item switch
        {
            null => "null",
            EntityUid uid => _ent.ToPrettyString(uid).ToString(),
            DictionaryEntry entry => $"{entry.Key}: {entry.Value}",
            _ => item.ToString() ?? "null",
        };

        return text.Length > MaxHintLength ? $"{text[..(MaxHintLength - 1)]}…" : text;
    }
}
