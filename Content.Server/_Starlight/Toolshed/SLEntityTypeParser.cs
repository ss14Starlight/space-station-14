using Content.Shared.GameTicking.Components;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class SLEntityTypeParser<TWrapper, TComponent> : TypeParser<TWrapper>
    where TWrapper : struct, IEntityStructWrapper<TComponent> where TComponent : IComponent
{
    [Dependency] private IEntityManager _entMan = default!;

    public static bool TryParseEntity(IEntityManager entMan, ParserContext ctx, out EntityUid result)
    {
        string? word;
        var start = ctx.Index;

        // e prefix implies we should parse the number as an EntityUid directly, not as a NetEntity
        // Note that this breaks auto completion results
        if (ctx.EatMatch('e'))
        {
            word = ctx.GetWord(ParserContext.IsToken);
            if (EntityUid.TryParse(word, out result))
                return true;

            ctx.Error = word is not null ? new InvalidEntity($"e{word}") : new OutOfInputError();
            ctx.Error.Contextualize(ctx.Input, (start, ctx.Index));
            return false;
        }

        // Optional 'n' prefix for differentiating whether an integer represents a NetEntity or EntityUid
        ctx.EatMatch('n');
        word = ctx.GetWord(ParserContext.IsToken);

        if (NetEntity.TryParse(word, out var ent))
        {
            result = entMan.GetEntity(ent);
            return true;
        }

        result = default;

        ctx.Error = word is not null ? new InvalidEntity(word) : new OutOfInputError();
        ctx.Error.Contextualize(ctx.Input, (start, ctx.Index));
        return false;
    }

    public static bool TryParse(IEntityManager entMan, ParserContext ctx, out TWrapper result)
    {
        result = default;
        if (!TryParseEntity(entMan, ctx, out var uid))
            return false;

        if (!entMan.TryGetComponent<TComponent>(uid, out var comp))
            return false;

        result = (TWrapper)TWrapper.Create((uid, comp));
        return true;
    }

    public static CompletionResult? TryAutocomplete(IEntityManager entMan, ParserContext ctx, CommandArgument? arg)
    {
        var hint = ToolshedCommand.GetArgHint(arg, typeof(TWrapper));
        var query = entMan.AllEntityQueryEnumerator<TComponent, MetaDataComponent>();
        var list = new List<CompletionOption>();
        while (query.MoveNext(out _, out var metadata))
            list.Add(new CompletionOption(metadata.NetEntity.ToString(), metadata.EntityPrototype!.ID));

        return CompletionResult.FromHintOptions(list, hint);
    }

    public override bool TryParse(ParserContext ctx, out TWrapper result) => TryParse(_entMan, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        TryAutocomplete(_entMan, ctx, arg);
}

public interface IEntityStructWrapper<TComponent> where TComponent : IComponent
{
    Entity<TComponent> Entity { get; }
    static abstract object Create(Entity<TComponent> entity);
}
