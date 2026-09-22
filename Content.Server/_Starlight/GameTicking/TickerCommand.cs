using System.Linq;
using System.Runtime.InteropServices;
using Content.Server._Starlight.Administration.Systems;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.GameTicking;

[ToolshedCommand]
[AdminCommand(AdminFlags.Round)]
public sealed class TickerCommand : ToolshedCommand
{
    private GameTicker? _ticker;
    private RoundEndSystem? _end;
    private AutoDiscordLogSystem? _log;

    #region RoundTiming

    /// End round without starting the restart timer.
    [CommandImplementation("endround")]
    public void EndRound(IInvocationContext ctx)
    {
        _ticker ??= GetSys<GameTicker>();
        if (_ticker.RunLevel == GameRunLevel.PostRound)
        {
            ctx.WriteMarkup("[color=yellow]The round has already been ended.[/color]");
            return;
        }
        _ticker.EndRound();
        ctx.WriteLine("The round has been ended.");
    }

    /// End round if it isn't ended already and start the restart timer. Will restart timer if already active.
    [CommandImplementation("restartround")]
    public void RestartRound(IInvocationContext ctx, [Optional] [DefaultParameterValue(-1f)] float countdownTime)
    {
        _end ??= GetSys<RoundEndSystem>();
        _ticker ??= GetSys<GameTicker>();
        TimeSpan? time = Math.Sign(countdownTime) > 0
            ? TimeSpan.FromSeconds(countdownTime)
            : null;
        if (_end.IsRestartTimerActive())
        {
            _end.CancelRoundRestartTimer(ctx.Session);
            _end.StartRestartTimer(time);
            ctx.WriteLine("Timer was restarted.");
            return;
        }

        if (_ticker.RunLevel == GameRunLevel.InRound)
        {
            _end.EndRound(time);
            ctx.WriteLine($"Round ended{(_end.StartTimerOnRestart ? ", restart timer enabled." : ".")}");
            return;
        }

        _end.StartRestartTimer(time);
        ctx.WriteLine("The timer has been started.");
    }

    /// Instantly end and restart the round, returning to lobby.
    [CommandImplementation("restartroundnow")]
    public void RestartRoundNow(IInvocationContext ctx)
    {
        _ticker ??= GetSys<GameTicker>();
        _ticker.RestartRound();
        ctx.WriteLine("Restarted round.");
    }

    /// Cancels the restart timer.
    [CommandImplementation("cancelrestart")]
    public void CancelRestartTimer(IInvocationContext ctx)
    {
        _end ??= GetSys<RoundEndSystem>();
        if (!_end.IsRestartTimerActive())
        {
            ctx.WriteMarkup("[color=yellow]Timer was not active.[/color]");
            return;
        }
        _end.CancelRoundRestartTimer(ctx.Session);
        ctx.WriteLine("Round timer has been cancelled.");
    }

    /// Cancels the post-round state, making the game act as though the round has not yet ended.
    [CommandImplementation("cancelpostround")]
    public void CancelPostRound(IInvocationContext ctx)
    {
        _ticker ??= GetSys<GameTicker>();
        _log ??= GetSys<AutoDiscordLogSystem>();
        _log.LogToDiscord($"Round end was cancelled by {ctx.Session?.Name ?? "unknown"}");
        _ticker.CancelPostRound(ctx.Session);
        ctx.WriteLine("Post-round has been cancelled.");
    }

    /// Toggles the automatic timer on round end.
    [CommandImplementation("toggletimeronend")]
    public void ToggleTimerOnend(IInvocationContext ctx, bool state)
    {
        _end ??= GetSys<RoundEndSystem>();
        _end.ToggleTimerOnEnd(state, ctx.Session);
        ctx.WriteLine($"The round restart timer will{(state ? " " : " NOT ")}start once round ends.");
    }

    /// Delay round start by a specified number of seconds, or pause if 0 or unspecified.
    [CommandImplementation("delaystart")]
    public void DelayStart(IInvocationContext ctx, [Optional] [DefaultParameterValue(0u)] uint seconds)
    {
        _ticker ??= GetSys<GameTicker>();
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            CommandMarkup.Error(ctx, "This command can only be ran in the lobby.");
            return;
        }

        if (seconds == 0)
        {
            ctx.WriteLine(Loc.GetString(_ticker.TogglePause() ? "cmd-delaystart-paused" : "cmd-delaystart-unpaused"));
            return;
        }

        if (!_ticker.DelayStart(TimeSpan.FromSeconds(seconds)))
            ctx.WriteLine(Loc.GetString("cmd-delaystart-too-late"));
    }

    #endregion

    #region GameRules

    /// Get a reference to an added gamerule entity.
    [CommandImplementation("getrule")]
    public EntityUid GetRule(IInvocationContext ctx,
        [CommandArgument(typeof(EntityWithCompCompletionParser<GameRuleComponent>))] EntityUid entity) => entity;

    /// Get all gamerules that are currently added.
    [CommandImplementation("getrules")]
    public IEnumerable<EntityUid> GetRules()
    {
        _ticker ??= GetSys<GameTicker>();
        return _ticker.GetAddedGameRules();
    }

    /// Get all added gamerule entities of a given rule prototype.
    [CommandImplementation("getrulesoftype")]
    public IEnumerable<EntityUid> GetRulesOfType(
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<GameRuleComponent>))] EntProtoId ruleId)
    {
        _ticker ??= GetSys<GameTicker>();
        return _ticker.GetAddedGameRules().Where(x => MetaData(x).EntityPrototype!.ID == ruleId);
    }

    /// Get all ACTIVE gamerules that are currently added.
    [CommandImplementation("getactiverules")]
    public IEnumerable<EntityUid> GetActiveRules()
    {
        _ticker ??= GetSys<GameTicker>();
        return _ticker.GetActiveGameRules();
    }

    /// Get all ACTIVE gamerule entities thar are currently added of a given rule prototype.
    [CommandImplementation("getactiverulesoftype")]
    public IEnumerable<EntityUid> GetActiveRulesOfType(
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<GameRuleComponent>))] EntProtoId ruleId)
    {
        _ticker ??= GetSys<GameTicker>();
        return _ticker.GetActiveGameRules().Where(x => MetaData(x).EntityPrototype!.ID == ruleId);
    }

    /// Add a gamerule entity prototype to the round.
    [CommandImplementation("addrule")]
    public EntityUid AddRule(IInvocationContext ctx,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<GameRuleComponent>))] EntProtoId ruleId)
    {
        _ticker ??= GetSys<GameTicker>();
        var uid = _ticker.AddGameRule(ruleId);
        ctx.WriteLine($"Added game rule {EntityManager.ToPrettyString(uid)}");
        return uid;
    }

    private EntityUid EndRuleDo(IInvocationContext ctx, EntityUid uid)
    {
        _ticker ??= GetSys<GameTicker>();
        if (HasComp<EndedGameRuleComponent>(uid))
        {
            CommandMarkup.Error(ctx, $"Game rule {EntityManager.ToPrettyString(uid)} has already ended.");
            return uid;
        }
        _ticker.EndGameRule(uid);
        ctx.WriteLine($"Ended game rule {EntityManager.ToPrettyString(uid)}");
        return uid;
    }

    /// End a gamerule entity's gamerule.
    [CommandImplementation("endrule")]
    public EntityUid EndRuleFiltered(IInvocationContext ctx,
        [CommandArgument(typeof(EntityWithCompCompletionParser<ActiveGameRuleComponent>))] EntityUid uid) =>
        EndRuleDo(ctx, uid);

    /// End a gamerule entity's gamerule. This one lets you pipe in an entity instead.
    [CommandImplementation("endrule")]
    public EntityUid EndRulePiped(IInvocationContext ctx, [PipedArgument] EntityUid uid) => EndRuleDo(ctx, uid);

    /// End a gamerule entity's gamerule. This one lets you pipe in a set of entities instead.
    [CommandImplementation("endrule")]
    public IEnumerable<EntityUid> EndRulePiped(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => EndRulePiped(ctx, x));

    #endregion
}
