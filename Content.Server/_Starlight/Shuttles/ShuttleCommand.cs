using System.Runtime.InteropServices;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Audio;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Shuttles;

[ToolshedCommand]
[AdminCommand(AdminFlags.Round)]
public sealed partial class ShuttleCommand : ToolshedCommand
{
    [Dependency] private IAdminLogManager _log = null!;

    private ChatSystem? _chat;
    private GameTicker? _ticker;
    private RoundEndSystem? _round;
    private EmergencyShuttleSystem? _eShuttle;

    /// Delay the departure of emergency shuttles by a specified number of seconds.
    [CommandImplementation("delayemergencyshuttledeparture")]
    public void DelayEmergencyShuttleDeparture(IInvocationContext ctx, float seconds,
        [Optional] [DefaultParameterValue(false)] bool sendAnnouncement,
        [Optional] [DefaultParameterValue("")] string additionalText)
    {
        _eShuttle ??= GetSys<EmergencyShuttleSystem>();
        if (!_eShuttle.EmergencyShuttleArrived || _eShuttle.ShuttlesLeft)
        {
            CommandMarkup.Error(ctx, "Emergency shuttles have already departed or have not arrived yet.");
            return;
        }
        _eShuttle.DelayShuttleDeparture(seconds, ctx.Session);
        if (sendAnnouncement)
        {
            var str = Loc.GetString("delayemergencyshuttledeparture-message", ("seconds", seconds));
            if (additionalText != string.Empty) str += $"\n{additionalText}";
            _chat ??= GetSys<ChatSystem>();
            _chat.DispatchGlobalAnnouncement(str, "Central Command");
        }

        ctx.WriteLine($"Delayed shuttle departure by {seconds} seconds.");
    }

    /// Delay the arrival of the emergency shuttle to station by a specified number of seconds.
    [CommandImplementation("delayemergencyshuttlearrival")]
    public void DelayEmergencyShuttleArrival(IInvocationContext ctx, float seconds,
        [Optional] [DefaultParameterValue(false)] bool sendAnnouncement,
        [Optional] [DefaultParameterValue("")] string additionalText)
    {
        _round ??= GetSys<RoundEndSystem>();
        _eShuttle ??= GetSys<EmergencyShuttleSystem>();
        _ticker ??= GetSys<GameTicker>();
        if (!_round.IsRoundEndRequested() || _eShuttle.EmergencyShuttleArrived || _ticker.RunLevel != GameRunLevel.InRound) // Need to be more careful with this one
        {
            CommandMarkup.Error(ctx, "Can't add time to shuttle arrival right now. Either already arrived or not in round.");
            return;
        }
        _eShuttle.DelayShuttleArrival(seconds, ctx.Session);
        if (sendAnnouncement)
        {
            var str = Loc.GetString("delayemergencyshuttlearrival-message", ("seconds", seconds));
            if (additionalText != string.Empty) str += $"\n{additionalText}";
            _chat ??= GetSys<ChatSystem>();
            _chat.DispatchGlobalAnnouncement(str, "Central Command");
        }

        ctx.WriteLine($"Delayed shuttle arrival by {seconds} seconds.");
    }

    /// Instantly dock the emergency shuttle.
    [AdminCommand(AdminFlags.Fun)]
    [CommandImplementation("dockemergencyshuttle")]
    public void DockEmergencyShuttle(IInvocationContext ctx)
    {
        _eShuttle ??= GetSys<EmergencyShuttleSystem>();
        if (_eShuttle.EmergencyShuttleArrived)
        {
            CommandMarkup.Error(ctx, "Emergency shuttles have already arrived.");
            return;
        }
        _eShuttle.DockEmergencyShuttle();
        _log.Add(LogType.AdminCommands,
            $"{CommandHelpers.PlayerNameOrServer(ctx)} has caused the emergency shuttle to dock prematurely.");
    }

    /// Call the emergency shuttle with an optional arrival time.
    [CommandImplementation("callemergencyshuttle")]
    public void CallEmergencyShuttle(IInvocationContext ctx, [Optional] [DefaultParameterValue(0f)] float seconds)
    {
        _round ??= GetSys<RoundEndSystem>();
        if (seconds == 0) _round.RequestRoundEnd(ctx.Session?.AttachedEntity, checkCooldown: false);
        else _round.RequestRoundEnd(TimeSpan.FromSeconds(seconds), ctx.Session?.AttachedEntity, checkCooldown: false);
    }

    /// <summary>
    /// Recall the emergency shuttle.
    /// </summary>
    [CommandImplementation("recallemergencyshuttle")]
    public void RecallEmergencyShuttle(IInvocationContext ctx)
    {
        _round ??= GetSys<RoundEndSystem>();
        _round.CancelRoundEndCountdown(ctx.Session?.AttachedEntity, forceRecall: true);
    }

    // TODO: Improve this once nullable type parsers exist
    /// <summary>
    /// Allow or disallow calling the emergency shuttle. Note that this persists between rounds.
    /// </summary>
    [CommandImplementation("allowemergencyshuttlecalls")]
    public void AllowEmergencyShuttleCalls(IInvocationContext ctx, bool state,
        [Optional] [DefaultParameterValue(false)] bool announce, [Optional] [DefaultParameterValue("")] string message,
        [Optional] [DefaultParameterValue("")] string sender, [Optional] [DefaultParameterValue("")] string color,
        [Optional] [DefaultParameterValue("")] string soundPath)
    {
        _round ??= GetSys<RoundEndSystem>();

        if (announce)
        {
            _chat ??= GetSys<ChatSystem>();
            var announcementText = message != string.Empty
                ? message
                : state
                    ? "Emergency shuttle calls have been enabled."
                    : "Emergency shuttle calls have been disabled.";

            var senderText = sender != string.Empty
                ? sender
                : "Central Command";

            var announcementColor = color != string.Empty
                ? Color.FromHex(color)
                : Color.Gold;

            var announcementSound = soundPath != string.Empty
                ? new SoundPathSpecifier(soundPath)
                : null;

            _chat.DispatchGlobalAnnouncement(announcementText, senderText, true, announcementSound, announcementColor);
        }

        _round.SetShuttleCallsEnabled(state);
        ctx.WriteLine($"{(state ? "Enabled" : "Disabled")} shuttle calls for the round.");
    }
}
