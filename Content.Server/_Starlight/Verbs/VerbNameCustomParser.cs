using System.Linq;
using Content.Server.Verbs;
using Content.Server._Starlight.Commands;
using Content.Shared.Verbs;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Verbs;

public sealed partial class VerbNameCustomParser : CustomCompletionParser<string>
{
    private const CompletionOptionFlags OptionFlags = CompletionOptionFlags.NoEscape | CompletionOptionFlags.NoQuote;

    [Dependency] private IEntitySystemManager _entSys = null!;
    private VerbSystem? _verb;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        _verb ??= _entSys.GetEntitySystem<VerbSystem>();

        // local vars can't be parsed so just do verb type instead I suppose????
        if (!TryResolveInvoker(ctx, out var invoker)
            || !TryResolveTargets(ctx, out var targets)
            || targets.FirstOrNull() is not { } target)
        {
            var typeOpts = Verb.VerbTypes
                .Select(type => new CompletionOption(Quote(type.Name), "verb type", OptionFlags));
            return CompletionResult.FromHintOptions(typeOpts, "verb name or type");
        }

        // just do first one and deal with consequences later godo
        var opts = _verb.GetLocalVerbs(target, invoker, Verb.VerbTypes, true)
            .Select(verb => verb.Text)
            .Distinct()
            .Select(text => new CompletionOption(Quote(text), Flags: OptionFlags));
        return CompletionResult.FromHintOptions(opts, "verb name");
    }

    private static string Quote(string text) => $"\"{text.Replace("\\", @"\\").Replace("\"", "\\\"")}\"";

    private static bool TryResolveInvoker(ParserContext ctx, out EntityUid invoker)
    {
        if (ctx.Bundle.Arguments?.ContainsKey("invoker") == true)
            return TryResolveEntity(ctx, "invoker", out invoker);

        invoker = ctx.Session?.AttachedEntity ?? default;
        return invoker.IsValid();
    }

    private static bool TryResolveTargets(ParserContext ctx, out IEnumerable<EntityUid> targets)
    {
        targets = [];
        if (ctx.Bundle.PipedType is null)
        {
            if (!TryResolveEntity(ctx, "target", out var target))
                return false;

            targets = [target];
            return true;
        }

        switch (CommandHelpers.EvaluatePipedInput(ctx))
        {
            case EntityUid target:
                targets = [target];
                return true;
            case IEnumerable<EntityUid> enumerable:
                targets = enumerable;
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveEntity(ParserContext ctx, string name, out EntityUid uid)
    {
        uid = default;
        if (ctx.Bundle.Arguments?.GetValueOrDefault(name) is not { } arg)
            return false;

        var value = arg switch
        {
            EntityUid ent => ent,
            VarRef<EntityUid> varRef => CommandHelpers.ReadVar(ctx, varRef.VarName),
            // Evaluating a block would execute commands, which we don't want to do while autocompleting.
            BlockRef<EntityUid> => null,
            // Only remaining case is a ParsedValueRef, whose evaluation does not use the invocation context.
            ValueRef<EntityUid> valueRef => valueRef.Evaluate(null!),
            _ => null,
        };

        if (value is not EntityUid resolved)
            return false;

        uid = resolved;
        return true;
    }
}
