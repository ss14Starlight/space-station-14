using System.Linq;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class EntityWithCompCompletionParser<TComponent> : CustomCompletionParser<EntityUid>
    where TComponent : IComponent
{
    [Dependency] private IEntityManager _entMan = null!;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var uids = _entMan.AllEntities<TComponent>();
        var hint = ToolshedCommand.GetArgHint(arg, typeof(EntityUid));
        var options = uids
            .Where(uid =>
                uid.Owner.Id.ToString().StartsWith(ctx.Input[ctx.Index..], StringComparison.OrdinalIgnoreCase))
            .Select(uid =>
            {
                var meta = _entMan.GetComponent<MetaDataComponent>(uid);
                return new CompletionOption(uid.Owner.Id.ToString(),
                    $"{(meta.EntityName != string.Empty
                        ? $"({meta.EntityName}) "
                        : string.Empty)}{(meta.EntityPrototype is not null
                        ? $"[{meta.EntityPrototype.ID}]"
                        : "[no prototype]")}");
            }).OrderBy(o => o.Value).ToList();
        return CompletionResult.FromHintOptions(options, hint);
    }
}
