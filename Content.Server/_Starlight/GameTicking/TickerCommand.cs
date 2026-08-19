using System.Runtime.InteropServices;
using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.GameTicking;

[ToolshedCommand]
[AdminCommand(AdminFlags.Round)]
public sealed class TickerCommand : ToolshedCommand
{
    private GameTicker? _ticker;
    private RoundEndSystem? _end;
    private AutoDiscordLogSystem? _log;

    /// <summary>
    /// End round without starting the restart timer.
    /// </summary>
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

    /// <summary>
    /// End round if it isn't ended already and start the restart timer. Will restart timer if already active.
    /// </summary>
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
            ctx.WriteLine("Round ended, restart timer enabled.");
            return;
        }

        _end.StartRestartTimer(time);
        ctx.WriteLine("The timer has been started.");
    }

    /// <summary>
    /// Instantly end and restart the round, returning to lobby.
    /// </summary>
    [CommandImplementation("restartroundnow")]
    public void RestartRoundNow(IInvocationContext ctx)
    {
        _ticker ??= GetSys<GameTicker>();
        _ticker.RestartRound();
        ctx.WriteLine("Restarted round.");
    }

    /// <summary>
    /// Cancels the restart timer.
    /// </summary>
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

    /// <summary>
    /// Cancels the post-round state, making the game act as though the round has not yet ended.
    /// </summary>
    [CommandImplementation("cancelpostround")]
    public void CancelPostRound(IInvocationContext ctx)
    {
        _ticker ??= GetSys<GameTicker>();
        _log ??= GetSys<AutoDiscordLogSystem>();
        _log.LogToDiscord($"Round end was cancelled by {ctx.Session?.Name ?? "unknown"}");
        _ticker.CancelPostRound(ctx.Session);
        ctx.WriteLine("Post-round has been cancelled.");
    }
}
