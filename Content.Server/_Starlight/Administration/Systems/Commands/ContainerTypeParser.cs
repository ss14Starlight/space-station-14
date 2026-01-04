using System.Diagnostics;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Administration.Commands;

public sealed class ContainerTypeParser : TypeParser<ContainerRef>
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private SharedContainerSystem? _container;
    
    public override bool TryParse(ParserContext ctx, out ContainerRef result)
    {
        _container ??= _entMan.System<SharedContainerSystem>();
        var word = ctx.GetWord(ParserContext.IsToken)?.ToLowerInvariant();
        if (word is null)
        {
            if (ctx.PeekRune() is null)
            {
                ctx.Error = new OutOfInputError();
                result = default;
                return false;
            }

            ctx.Error = new InvalidContainer(ctx.GetWord()!);
            result = default;
            return false;
        }

        if (ctx.EatMatch("ContainerRef:"))
        {
            var owner = ctx.GetWord(ParserContext.IsToken);
            if (EntityUid.TryParse(owner, out var uid))
            {
                if (ctx.EatMatch($"{owner}/"))
                {
                    var containerId = ctx.GetWord(ParserContext.IsToken);
                    if (containerId is null)
                    {
                        result = new ContainerRef(null);
                        return false;
                    }
                    if (_container.TryGetContainer(uid, containerId, out var container))
                    {
                        result = new ContainerRef(container);
                        return true;
                    }
                }
            }
        }
        
        result = new ContainerRef(null);
        return false;
    }

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        CompletionResult.Empty;
}

public record InvalidContainer(string Value) : IConError
{
    public FormattedMessage DescribeInner() =>
        FormattedMessage.FromUnformatted(
            $"The value {Value} is not a valid container.");

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
