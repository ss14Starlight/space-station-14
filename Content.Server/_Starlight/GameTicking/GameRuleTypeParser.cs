using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.GameTicking;

public sealed partial class GameRuleTypeParser : TypeParser<GameRuleProtoId>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;
    private GameTicker? _ticker;

    public override bool TryParse(ParserContext ctx, out GameRuleProtoId result)
    {
        result = default;
        if (!Toolshed.TryParse(ctx, out ProtoId<EntityPrototype> proto))
            return false;

        _ticker ??= _entMan.System<GameTicker>();
        if (!_ticker.GetAllGameRulePrototypes().Contains(_proto.Index(proto)))
            return false;

        result = new GameRuleProtoId(proto.Id);
        return true;
    }

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        _ticker ??= _entMan.System<GameTicker>();
        var rules = _ticker.GetAllGameRulePrototypes().ToList();
        var hint = ToolshedCommand.GetArgHint(arg, typeof(GameRuleProtoId));
        var options = rules.Where(rule => rule.ID.StartsWith(ctx.Input[ctx.Index..], StringComparison.OrdinalIgnoreCase))
            .Select(rule => new CompletionOption(rule.ID)).OrderBy(o => o.Value).ToList();
        return CompletionResult.FromHintOptions(options, hint);
    }
}

public sealed partial class GameRuleEntityTypeParser : TypeParser<GameRuleEntity>
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

    public override bool TryParse(ParserContext parser, out GameRuleEntity result)
    {
        result = default;
        if (!TryParseEntity(_entMan, parser, out var uid))
            return false;

        if (!_entMan.TryGetComponent<GameRuleComponent>(uid, out var comp))
            return false;

        result = new GameRuleEntity(new Entity<GameRuleComponent>(uid, comp));
        return true;
    }

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var hint = ToolshedCommand.GetArgHint(arg, typeof(GameRuleEntity));
        var query = _entMan.AllEntityQueryEnumerator<GameRuleComponent, MetaDataComponent>();
        var list = new List<CompletionOption>();
        while (query.MoveNext(out _, out var metadata))
        {
            list.Add(new CompletionOption(metadata.NetEntity.ToString(), metadata.EntityPrototype!.ID)); // Use prototype here instead of name since game rule entities are unnamed.
        }

        return CompletionResult.FromHintOptions(list, hint);
    }
}

public sealed partial class ActiveGameRuleEntityTypeParser : TypeParser<ActiveGameRuleEntity>
{
    [Dependency] private IEntityManager _entMan = default!;

    public override bool TryParse(ParserContext parser, out ActiveGameRuleEntity result)
    {
        result = default;
        if (!GameRuleEntityTypeParser.TryParseEntity(_entMan, parser, out var uid))
            return false;

        if (!_entMan.TryGetComponent<GameRuleComponent>(uid, out var comp))
            return false;

        result = new ActiveGameRuleEntity(new Entity<GameRuleComponent>(uid, comp));
        return true;
    }

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var hint = ToolshedCommand.GetArgHint(arg, typeof(ActiveGameRuleEntity));
        var query = _entMan.AllEntityQueryEnumerator<GameRuleComponent, MetaDataComponent>();
        var list = new List<CompletionOption>();
        while (query.MoveNext(out var uid, out _, out var metadata))
        {
            if (_entMan.HasComponent<EndedGameRuleComponent>(uid)) continue; // Prevent ended game rules from populating list
            list.Add(new CompletionOption(metadata.NetEntity.ToString(), metadata.EntityPrototype!.ID)); // Use prototype here instead of name since game rule entities are unnamed.
        }

        return list.Count == 0
            ? new CompletionResult([], "[There are no active game rules.]")
            : CompletionResult.FromHintOptions(list, hint);
    }
}

public readonly record struct GameRuleProtoId(EntProtoId ProtoId);
public readonly record struct GameRuleEntity(Entity<GameRuleComponent> Entity);
public readonly record struct ActiveGameRuleEntity(Entity<GameRuleComponent> Entity);
