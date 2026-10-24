using System.Linq;
using Content.Shared.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class EntProtoIdWithCompCompletionParser<TComponent> : CustomCompletionParser<EntProtoId>
    where TComponent : IComponent
{
    [Dependency] private IPrototypeManager _proto = null!;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var prototypes = _proto.EnumeratePrototypes<EntityPrototype>().Where(proto => !proto.Abstract)
            .Where(proto => proto.HasComponent<TComponent>());
        var hint = ToolshedCommand.GetArgHint(arg, typeof(EntProtoId));
        var options = prototypes
            .Where(proto => proto.ID.StartsWith(ctx.Input[ctx.Index..], StringComparison.OrdinalIgnoreCase))
            .Select(proto => new CompletionOption(proto.ID)).OrderBy(o => o.Value).ToList();
        return CompletionResult.FromHintOptions(options, hint);
    }
}
