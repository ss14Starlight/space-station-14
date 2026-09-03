using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.GameTicking;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class RuleCommand : ToolshedCommand
{
    private GameTicker? _ticker;

    [CommandImplementation("get")]
    public EntityUid GetRule(IInvocationContext ctx, GameRuleEntity entity) => entity.Entity;

    [CommandImplementation("gettype")]
    public IEnumerable<EntityUid> GetRulesOfType(IInvocationContext ctx, GameRuleProtoId ruleId)
    {
        _ticker ??= GetSys<GameTicker>();
        var rules = _ticker.GetAddedGameRules().Where(x => MetaData(x).EntityPrototype!.ID == ruleId.ProtoId).ToList();
        if (rules.Count == 0) ctx.WriteMarkup($"[color=gold]No rules with protoId \"{ruleId.ProtoId.Id}\" found, returned list is empty.[/color]");
        return rules;
    }

    [CommandImplementation("add")]
    public EntityUid AddRule(IInvocationContext ctx, GameRuleProtoId ruleId)
    {
        _ticker ??= GetSys<GameTicker>();
        var uid = _ticker.AddGameRule(ruleId.ProtoId);
        ctx.WriteLine($"Added game rule {EntityManager.ToPrettyString(uid)}");
        return uid;
    }

    private EntityUid EndRuleDo(IInvocationContext ctx, EntityUid uid)
    {
        _ticker ??= GetSys<GameTicker>();
        if (HasComp<EndedGameRuleComponent>(uid))
        {
            ctx.WriteMarkup($"[color=red]Game rule {EntityManager.ToPrettyString(uid)} has already ended.[/color]");
            return uid;
        }
        _ticker.EndGameRule(uid);
        ctx.WriteLine($"Ended game rule {EntityManager.ToPrettyString(uid)}");
        return uid;
    }

    [CommandImplementation("end")]
    public EntityUid EndRule(IInvocationContext ctx, ActiveGameRuleEntity uid) => EndRuleDo(ctx, uid.Entity);

    // alt where you can pipe in one instead for use with get or gettype
    [CommandImplementation("end")]
    public EntityUid EndRule(IInvocationContext ctx, [PipedArgument] EntityUid uid) => EndRuleDo(ctx, uid);

    // enumerable variant of above
    [CommandImplementation("end")]
    public IEnumerable<EntityUid> EndRulesOfType(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => EndRule(ctx, x));
}
