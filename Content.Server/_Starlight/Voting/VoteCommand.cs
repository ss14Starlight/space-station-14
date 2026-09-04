using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Content.Server._Starlight.Commands;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Discord.WebhookMessages;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Random.Helpers;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Voting;

[ToolshedCommand]
[AdminCommand(AdminFlags.Round)]
public sealed partial class VoteCommand : ToolshedCommand
{
    [Dependency] private VoteWebhooks _webhook = default!;
    [Dependency] private IVoteManager _vote = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _plr = default!;
    [Dependency] private IGameMapManager _map = default!;
    [Dependency] private IAdminLogManager _aLog = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    private GameTicker? _ticker;

    private const string SecretPrototype = "Secret";

    private static readonly Counter _gamemodeVote = Metrics.CreateCounter(
        "sl_gamemode_vote",
        "Gamemode vote results",
        [ "option" ]
    );

    private static readonly Counter _mapVote = Metrics.CreateCounter(
        "sl_map_vote",
        "Map/Station vote results",
        [ "option" ]
    );

    private List<ICommonSession>? GetSessionsFromEntities(IInvocationContext ctx, IEnumerable<EntityUid> voters)
    {
        List<ICommonSession> sessions = [];
        foreach (var voter in voters)
            if (_plr.TryGetSessionByEntity(voter, out var session))
                sessions.Add(session);
        if (sessions.Count != 0) return sessions;
        CommandMarkup.Error(ctx, $"No valid sessions were found attached to the piped entities.");
        return null;
    }

    [CommandImplementation("cancel")]
    public void CancelVote(IInvocationContext ctx, [CommandArgument(typeof(VoteIdCompletionParser))] int id)
    {
        if (!_vote.TryGetVote(id, out var vote))
        {
            CommandMarkup.Error(ctx, $"No vote with ID ${id} was found.");
            return;
        }

        vote.Cancel();
        ctx.WriteLine($"Cancelled vote with ID ${id}.");
    }

    [CommandImplementation("cancelall")]
    public void CancelAll(IInvocationContext ctx)
    {
        foreach (var vote in _vote.ActiveVotes)
            vote.Cancel();

        ctx.WriteLine("Cancelled all active votes.");
    }

    // TODO: PR RobustToolbox with the ability to parse nullable things and then implement the targeting feature.
    private (IVoteHandle handle, VoteWebhooks.WebhookState? webhookState) CreateVote(IInvocationContext ctx, string title,
        [ListLength(MinLength = 2, MaxLength = 9)] List<(string, object)> options, List<ICommonSession>? voters,
        bool showVotes = true, float duration = 30, EntityUid? target = null)
    {
        var voteOptions = new VoteOptions
        {
            DisplayVotes = showVotes,
            Duration = TimeSpan.FromSeconds(duration),
            Options = options,
            Title = title
        };

        if (voters is not null)
        {
            var voterSet = voters.ToHashSet();
            voterSet.UnionWith([.. _admin.ActiveAdmins]); // Always include active admins in the vote
            voteOptions.PlayerFilter = [.. voterSet];
            voteOptions.VoterEligibility = VoteManager.VoterEligibility.Filter;
        }
        else
            voteOptions.VoterEligibility = VoteManager.VoterEligibility.All;

        if (target is not null) voteOptions.TargetEntity = EntityManager.GetNetEntity(target.Value);
        voteOptions.SetInitiatorOrServer(ctx.Session);

        var handle = _vote.CreateVote(voteOptions);
        var webhookState = _webhook.CreateWebhookIfConfigured(voteOptions, _cfg.GetCVar(CCVars.DiscordVoteWebhook));

        ctx.WriteLine($"Created a new vote with the ID {handle.Id}.");
        return (handle, webhookState);
    }

    private void CreateCustomVote(IInvocationContext ctx, string title,
        [ListLength(MinLength = 2, MaxLength = 9)] List<string> options, List<ICommonSession>? voters,
        bool showVotes = true, float duration = 30, EntityUid? target = null)
    {
        var chatFilter = Filter.Empty();
        List<(string, object)> voteOptions = [.. options.Select((t, i) => (t, i))];
        var (handle, webhookState) = CreateVote(ctx, title, voteOptions, voters, showVotes, duration, target);

        if (ctx.Session is not null)
            _aLog.Add(LogType.Vote, LogImpact.Medium,
                $"{ctx.Session} initiated a custom vote: {title} - {string.Join("; ", voteOptions.Select(x => x.Item1))}");
        else
            _aLog.Add(LogType.Vote, LogImpact.Medium,
                $"Initiated a custom vote: {title} - {string.Join("; ", voteOptions.Select(x => x.Item1))}");

        if (voters is not null) chatFilter.AddPlayers(voters);
        else chatFilter.AddAllPlayers();

        handle.OnFinished += (_, eventArgs) =>
        {
            if (eventArgs.Winner == null)
            {
                var ties = string.Join(", ", eventArgs.Winners.Select(c => options[(int) c]));
                _aLog.Add(LogType.Vote, LogImpact.Medium, $"Custom vote {title} finished as tie: {ties}");
                var message = Loc.GetString("cmd-customvote-on-finished-tie", ("title", title), ("ties", ties));
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }
            else
            {
                _aLog.Add(LogType.Vote, LogImpact.Medium,
                    $"Custom vote {title} finished: {options[(int)eventArgs.Winner]}");
                var message = Loc.GetString("cmd-customvote-on-finished-win", ("title", title),
                    ("winner", options[(int)eventArgs.Winner]));
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }

            _webhook.UpdateWebhookIfConfigured(webhookState, eventArgs);
        };

        handle.OnCancelled += _ =>
        {
            _webhook.UpdateCancelledWebhookIfConfigured(webhookState);
        };
    }

    [CommandImplementation("custom")]
    public IEnumerable<ICommonSession> CreateCustomVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<ICommonSession> sessions,
        string title, [ListLength(MinLength = 1, MaxLength = 9)] List<string> options,
        [Optional] [DefaultParameterValue(true)]
        bool showVotes,
        [Optional] [DefaultParameterValue(30f)]
        float duration)
    {
        var voters = sessions.ToList();
        CreateCustomVote(ctx, title, options, voters, showVotes, duration);
        return voters;
    }

    [CommandImplementation("custom")]
    public IEnumerable<EntityUid> CreateCustomVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uids,
        string title, [ListLength(MinLength = 1, MaxLength = 9)] List<string> options,
        [Optional] [DefaultParameterValue(true)]
        bool showVotes, [Optional] [DefaultParameterValue(30f)] float duration)
    {
        var voters = uids.ToList();
        CreateCustomVote(ctx, title, options, GetSessionsFromEntities(ctx, voters), showVotes, duration);
        return voters;
    }

    [CommandImplementation("custom")]
    public void CreateCustomVote(IInvocationContext ctx, string title,
        [ListLength(MinLength = 1, MaxLength = 9)] List<string> options,
        [Optional] [DefaultParameterValue(true)] bool showVotes,
        [Optional] [DefaultParameterValue(30f)] float duration) =>
        CreateCustomVote(ctx, title, options, null, showVotes, duration);

    private void SendWinnerMessage(string message, string wrappedMessage, Filter filter) =>
        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Server, message, wrappedMessage, EntityUid.Invalid, false,
            true, null);

    private void CreateMapVote(IInvocationContext ctx, List<ProtoId<GameMapPrototype>> options, List<ICommonSession>? voters = null,
        bool includeSecret = false, float duration = 0, string title = "")
    {
        List<GameMapPrototype> mapPrototypes = [];
        foreach (var protoId in options)
        {
            if (!_proto.TryIndex(protoId, out var map)) continue;
            mapPrototypes.Add(map);
        }

        List<(string, object)> maps = [];
        if (mapPrototypes.Count == 0)
        {
            var eligibleMaps = _map.CurrentlyEligibleMaps().ToList();
            var selectedMaps = eligibleMaps.OrderBy(_ => _random.Next())
                .Take(_cfg.GetCVar(StarlightCCVars.MapVotingCount)).ToList();
            if (includeSecret) maps.Add((Loc.GetString("ui-vote-secret-map"), _random.Pick(selectedMaps)));
            maps.AddRange(selectedMaps.Select(map => (map.MapName, map)).Select(dummy => ((string, object))dummy));
        }
        else
        {
            if (includeSecret) maps.Add((Loc.GetString("ui-vote-secret-map"), _random.Pick(mapPrototypes)));
            maps.AddRange(mapPrototypes.Select(map => (map.MapName, map)).Select(dummy => ((string, object))dummy));
        }

        if (title == string.Empty) title = Loc.GetString("ui-vote-map-title");

        var (handle, _) = CreateVote(ctx, title, maps, null, _cfg.GetCVar(StarlightCCVars.ShowMapVotes),
            duration != 0 ? duration : _cfg.GetCVar(CCVars.VoteTimerMap));

        var chatFilter = Filter.Empty();
        if (voters is not null) chatFilter.AddPlayers(voters);
        else chatFilter.AddAllPlayers();

        // plucked from original implementation
        handle.OnFinished += (_, args) =>
        {
            GameMapPrototype picked;
            if (args.Winner == null)
            {
                picked = (GameMapPrototype)_random.Pick(args.Winners);
                var message = Loc.GetString("ui-vote-map-tie");
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }
            else
                picked = (GameMapPrototype)args.Winner;

            {
                var message = Loc.GetString("ui-vote-map-win");
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }

            _aLog.Add(LogType.Vote, LogImpact.Medium, $"Map vote finished: {picked.MapName}");
            _ticker ??= EntityManager.System<GameTicker>();
            if (_ticker.CanUpdateMap())
            {
                for (var i = 0; i < maps.Count; i++)
                    _mapVote.WithLabels(
                        maps[i].Item1
                    ).Inc(args.Votes[i]);

                if (_map.CheckMapExists(picked.ID))
                {
                    _map.SelectMap(picked.ID);
                    _ticker.UpdateInfoText();
                }
                else
                {
                    var message = Loc.GetString("ui-vote-map-invalid",
                        ("winner", maps.Where(tuple => tuple.Item1 == picked.ID)));
                    SendWinnerMessage(message,
                        Loc.GetString("chat-manager-server-wrap-message",
                            ("message", FormattedMessage.EscapeText(message))), chatFilter);
                }
            }
            else
            {
                if (_ticker.RoundPreloadTime <= TimeSpan.Zero)
                {
                    var message = Loc.GetString("ui-vote-map-notlobby");
                    SendWinnerMessage(message,
                        Loc.GetString("chat-manager-server-wrap-message",
                            ("message", FormattedMessage.EscapeText(message))), chatFilter);
                }
                else
                {
                    var timeString = $"{_ticker.RoundPreloadTime.Minutes:0}:{_ticker.RoundPreloadTime.Seconds:00}";
                    var message = Loc.GetString("ui-vote-map-notlobby-time",
                        ("time", timeString));
                    SendWinnerMessage(message,
                        Loc.GetString("chat-manager-server-wrap-message",
                            ("message", FormattedMessage.EscapeText(message))), chatFilter);
                }
            }
        };
    }

    [CommandImplementation("map")]
    public IEnumerable<ICommonSession> CreateMapVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<ICommonSession> sessions, List<ProtoId<GameMapPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret,
        [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title)
    {
        var voters = sessions.ToList();
        CreateMapVote(ctx, options, voters, includeSecret, duration, title);
        return voters;
    }

    [CommandImplementation("map")]
    public IEnumerable<EntityUid> CreateMapVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uids, List<ProtoId<GameMapPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret,
        [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title)
    {
        var voters = uids.ToList();
        CreateMapVote(ctx, options, GetSessionsFromEntities(ctx, voters), includeSecret, duration, title);
        return voters;
    }

    [CommandImplementation("map")]
    public void CreateMapVote(IInvocationContext ctx, List<ProtoId<GameMapPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret, [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title) =>
        CreateMapVote(ctx, options, null, includeSecret, duration, title);

    private void CreatePresetVote(IInvocationContext ctx, List<ProtoId<GamePresetPrototype>> options,
        List<ICommonSession>? voters, bool includeSecret = false, float duration = 0, string title = "")
    {
        if (_vote is not VoteManager mgr) return;

        List<GamePresetPrototype> presetPrototypes = [];
        foreach (var protoId in options)
        {
            if (!_proto.TryIndex(protoId, out var preset)) continue;
            presetPrototypes.Add(preset);
        }

        List<(string, object)> presets = [];
        GamePresetPrototype? secretPreset = null;
        var secret = includeSecret && _proto.TryIndex(SecretPrototype, out secretPreset);
        if (presetPrototypes.Count == 0)
        {
            var selectedPresets = mgr.GetGamePresets();
            if (secret) presets.Add((Loc.GetString("ui-vote-secret-map"), secretPreset!));
            presets.AddRange(selectedPresets.Select(preset => (Loc.GetString(preset.Value), preset.Key))
                .Select(dummy => ((string, object))dummy));
        }
        else
        {
            if (includeSecret) presets.Add((Loc.GetString("ui-vote-secret-map"), secretPreset!));
            presets.AddRange(presetPrototypes.Select(map => (Loc.GetString(map.ModeTitle), map))
                .Select(dummy => ((string, object))dummy));
        }

        if (title == string.Empty) title = Loc.GetString("ui-vote-gamemode-title");

        var (handle, _) = CreateVote(ctx, title, presets, voters, _cfg.GetCVar(StarlightCCVars.ShowPresetVotes),
            duration != 0 ? duration : _cfg.GetCVar(CCVars.VoteTimerPreset));

        var chatFilter = Filter.Empty();
        if (voters is not null) chatFilter.AddPlayers(voters);
        else chatFilter.AddAllPlayers();

        handle.OnFinished += (_, args) =>
        {
            string picked;
            GamePresetPrototype pickedPreset;
            if (args.Winner == null)
            {
                pickedPreset = (GamePresetPrototype)_random.Pick(args.Winners);
                picked = pickedPreset.ModeTitle;
                var message = Loc.GetString("ui-vote-gamemode-tie",
                    ("picked", Loc.GetString(pickedPreset.ModeTitle)));
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }
            else
            {
                pickedPreset = (GamePresetPrototype)args.Winner;
                picked = pickedPreset.ModeTitle;
                var message = Loc.GetString("ui-vote-gamemode-win",
                    ("winner", Loc.GetString(pickedPreset.ModeTitle)));
                SendWinnerMessage(message,
                    Loc.GetString("chat-manager-server-wrap-message",
                        ("message", FormattedMessage.EscapeText(message))), chatFilter);
            }

            _aLog.Add(LogType.Vote, LogImpact.Medium, $"Preset vote finished: {picked}");
            _ticker ??= EntityManager.System<GameTicker>();

            for (var i = 0; i < presets.Count; i++)
                _gamemodeVote.WithLabels(
                    presets[i].Item1
                ).Inc(args.Votes[i]);

            mgr.DecrementPresetCooldown(pickedPreset);
            mgr.AddPresetToCooldown(pickedPreset);

            _ticker.SetGamePreset(pickedPreset.ID);
        };
    }

    [CommandImplementation("preset")]
    public IEnumerable<ICommonSession> CreatePresetVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<ICommonSession> sessions, List<ProtoId<GamePresetPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret,
        [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title)
    {
        var voters = sessions.ToList();
        CreatePresetVote(ctx, options, voters, includeSecret, duration, title);
        return voters;
    }

    [CommandImplementation("preset")]
    public IEnumerable<EntityUid> CreatePresetVote(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uids, List<ProtoId<GamePresetPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret,
        [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title)
    {
        var voters = uids.ToList();
        CreatePresetVote(ctx, options, GetSessionsFromEntities(ctx, voters), includeSecret, duration, title);
        return voters;
    }

    [CommandImplementation("preset")]
    public void CreatePresetVote(IInvocationContext ctx, List<ProtoId<GamePresetPrototype>> options,
        [Optional] [DefaultParameterValue(false)]
        bool includeSecret, [Optional] [DefaultParameterValue(0f)] float duration,
        [Optional] [DefaultParameterValue("")] string title) =>
        CreatePresetVote(ctx, options, null, includeSecret, duration, title);
}
