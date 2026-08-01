using System.Linq;
using Content.Server._Starlight.Roles;
using Content.Shared.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class EntProtoIdCompTypeParser<TWrapper, TComponent> : TypeParser<TWrapper>
    where TWrapper : struct, IEntProtoIdCompStructWrapper where TComponent : IComponent
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public static bool TryParse(ToolshedManager toolshed, IPrototypeManager protoMan,
        ParserContext ctx, out TWrapper result)
    {
        result = default;
        if (!toolshed.TryParse(ctx, out ProtoId<EntityPrototype> proto))
            return false;

        if (!GetAllPrototypesWithComponent<TComponent>(protoMan).Contains(protoMan.Index(proto)))
            return false;

        result = (TWrapper)TWrapper.Create(new EntProtoId(proto));
        return true;
    }

    public static CompletionResult? TryAutocomplete(IPrototypeManager protoMan, ParserContext ctx, CommandArgument? arg)
    {
        var rules = GetAllPrototypesWithComponent<TComponent>(protoMan).ToList();
        var hint = ToolshedCommand.GetArgHint(arg, typeof(MindRoleProtoId));
        var options = rules
            .Where(rule => rule.ID.StartsWith(ctx.Input[ctx.Index..], StringComparison.OrdinalIgnoreCase))
            .Select(rule => new CompletionOption(rule.ID)).OrderBy(o => o.Value).ToList();
        return CompletionResult.FromHintOptions(options, hint);
    }

    public static IEnumerable<EntityPrototype> GetAllPrototypesWithComponent<T>(IPrototypeManager protoMan)
        where T : TComponent => protoMan.EnumeratePrototypes<EntityPrototype>().Where(proto => !proto.Abstract)
        .Where(proto => proto.HasComponent<T>());

    public override bool TryParse(ParserContext ctx, out TWrapper result) =>
        TryParse(Toolshed, _proto, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        TryAutocomplete(_proto, ctx, arg);
}

public interface IEntProtoIdCompStructWrapper
{
    EntProtoId ProtoId { get; }
    static abstract object Create(EntProtoId protoId);
}
