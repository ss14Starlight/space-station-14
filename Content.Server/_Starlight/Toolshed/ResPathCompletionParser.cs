using System.Linq;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Toolshed;

// TODO: Engine PR this as a type parser.
public sealed partial class ResPathCompletionParser : CustomCompletionParser<ResPath>
{
    [Dependency] private IResourceManager _rsc = null!;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        ctx.ConsumeWhitespace();
        var path = ctx.Input[ctx.Index..].Trim('"');

        var opts = CompletionHelper.UserFilePath(path, _rsc.UserData)
            .Concat(CompletionHelper.ContentFilePath(path, _rsc))
            .DistinctBy(opt => opt.Value)
            .Select(opt => (opt.Flags & CompletionOptionFlags.PartialCompletion) != 0
                ? new CompletionOption($"\"{opt.Value}", opt.Hint,
                    opt.Flags | CompletionOptionFlags.NoEscape | CompletionOptionFlags.NoQuote)
                : new CompletionOption($"\"{opt.Value}\"", opt.Hint,
                    opt.Flags | CompletionOptionFlags.NoEscape | CompletionOptionFlags.NoQuote));

        return CompletionResult.FromHintOptions(opts, GetArgHint(arg));
    }
}
