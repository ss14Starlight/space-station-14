using System.Linq;
using Content.Server.GameTicking;
using Content.Server._Starlight.Toolshed;
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
    private GameTicker? _ticker;

    public override bool TryParse(ParserContext ctx, out GameRuleProtoId result) =>
        EntProtoIdCompTypeParser<GameRuleProtoId, GameRuleComponent>.TryParse(Toolshed, _proto, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        EntProtoIdCompTypeParser<GameRuleProtoId, GameRuleComponent>.TryAutocomplete(_proto, ctx, arg);
}

public sealed partial class GameRuleEntityTypeParser : TypeParser<GameRuleEntity>
{
    [Dependency] private IEntityManager _entMan = default!;

    public override bool TryParse(ParserContext ctx, out GameRuleEntity result) =>
        SLEntityTypeParser<GameRuleEntity, GameRuleComponent>.TryParse(_entMan, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        SLEntityTypeParser<GameRuleEntity, GameRuleComponent>.TryAutocomplete(_entMan, ctx, arg);
}

public sealed partial class ActiveGameRuleEntityTypeParser : TypeParser<ActiveGameRuleEntity>
{
    [Dependency] private IEntityManager _entMan = default!;

    public override bool TryParse(ParserContext parser, out ActiveGameRuleEntity result)
    {
        result = default;
        if (!SLEntityTypeParser<GameRuleEntity, GameRuleComponent>.TryParseEntity(_entMan, parser, out var uid))
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

public readonly record struct GameRuleProtoId(EntProtoId ProtoId) : IEntProtoIdCompStructWrapper
{
    public static object Create(EntProtoId protoId) => new GameRuleProtoId(protoId);
}
public readonly record struct GameRuleEntity(Entity<GameRuleComponent> Entity) : IEntityStructWrapper<GameRuleComponent>
{
    public static object Create(Entity<GameRuleComponent> entity) => new GameRuleEntity(entity);
}

public readonly record struct ActiveGameRuleEntity(Entity<GameRuleComponent> Entity);
