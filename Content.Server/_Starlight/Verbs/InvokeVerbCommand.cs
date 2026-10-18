using System.Linq;
using Content.Server.Administration;
using Content.Server.Verbs;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Verbs;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class InvokeVerbCommand : ToolshedCommand
{
    private VerbSystem? _verb;

    private void InvokeVerbDo(IInvocationContext ctx, EntityUid invoker, EntityUid target, string verbName)
    {
        _verb ??= GetSys<VerbSystem>();
        var verbs = _verb.GetLocalVerbs(target, invoker, Verb.VerbTypes, true);

        // if the "verb name" is actually a verb-type, try to run any verb of that type.
        var verbType = Verb.VerbTypes.FirstOrDefault(x => string.Equals(x.Name, verbName, StringComparison.OrdinalIgnoreCase));
        if (verbType != null)
        {
            var verb = verbs.FirstOrDefault(v => v.GetType() == verbType);
            if (verb != null)
            {
                _verb.ExecuteVerb(verb, invoker, target, forced: true);
                ctx.WriteLine(Robust.Shared.Localization.Loc.GetString("invoke-verb-command-success",
                    ("verb", verbName), ("target", target), ("player", invoker)));
                return;
            }
        }

        foreach (var verb in verbs.Where(verb => string.Equals(verb.Text, verbName, StringComparison.OrdinalIgnoreCase)))
        {
            _verb.ExecuteVerb(verb, invoker, target, forced: true);
            ctx.WriteLine(Loc.GetString("invoke-verb-command-success", ("verb", verb.Text), ("target", target),
                ("player", invoker)));
            return;
        }

        CommandMarkup.Error(ctx,
            Loc.GetString("invoke-verb-command-verb-not-found", ("verb", verbName), ("target", target)));
    }

    [CommandImplementation]
    public EntityUid InvokeVerb(IInvocationContext ctx, [PipedArgument] EntityUid target,
        [CommandArgument(typeof(VerbNameCustomParser))] string verbName)
    {
        if (SharedCommandHelpers.NoSession(ctx)) return target;
        if (ctx.Session!.AttachedEntity is null)
        {
            CommandMarkup.Error(ctx, "You are not currently controlling any entity.");
            return target;
        }
        _verb ??= GetSys<VerbSystem>();
        InvokeVerbDo(ctx, ctx.Session.AttachedEntity.Value, target, verbName);
        return target;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> InvokeVerb(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> target,
        [CommandArgument(typeof(VerbNameCustomParser))] string verbName) =>
        target.Select(x => InvokeVerb(ctx, x, verbName));

    [CommandImplementation]
    public EntityUid InvokeVerb(IInvocationContext ctx, EntityUid invoker, EntityUid target,
        [CommandArgument(typeof(VerbNameCustomParser))] string verbName)
    {
        InvokeVerbDo(ctx, invoker, target, verbName);
        return target;
    }
}
