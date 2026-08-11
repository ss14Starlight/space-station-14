using System.Linq;
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed partial class JobWhitelistAddCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override string Command => "jobwhitelistadd";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var player = args[0].Trim();
        var roleId = args[1].Trim();

        string roleName;
        if (_prototypes.TryIndex<JobPrototype>(roleId, out var jobPrototype))
        {
            roleName = jobPrototype.LocalizedName;
        }
        else if (_prototypes.TryIndex<AntagPrototype>(roleId, out var antagPrototype))
        {
            roleName = Loc.GetString(antagPrototype.Name);
        }
        else
        {
            shell.WriteError(Loc.GetString("cmd-jobwhitelist-job-does-not-exist", ("job", roleId)));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data == null)
        {
            shell.WriteError(Loc.GetString("cmd-jobwhitelist-player-not-found", ("player", player)));
            return;
        }

        var guid = data.UserId;

        // use string roleId APIs (from steps 3–4), not job-only APIs
        if (await _db.IsRoleWhitelisted(guid, roleId))
        {
            shell.WriteLine(Loc.GetString("cmd-jobwhitelistadd-already-whitelisted",
                ("player", player),
                ("jobId", roleId),
                ("jobName", roleName)));
            return;
        }

        _jobWhitelist.AddWhitelist(guid, roleId);  // string overload from step 4
        shell.WriteLine(Loc.GetString("cmd-jobwhitelistadd-added",
            ("player", player),
            ("jobId", roleId),
            ("jobName", roleName)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-jobwhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            var options = _prototypes.EnumeratePrototypes<JobPrototype>().Select(p => p.ID)
            .Concat(_prototypes.EnumeratePrototypes<AntagPrototype>().Select(p => p.ID));

            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-jobwhitelist-hint-job"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed partial class GetJobWhitelistCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _players = default!;

    public override string Command => "jobwhitelistget";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("This command needs at least one argument.");
            shell.WriteLine(Help);
            return;
        }

        var player = string.Join(' ', args).Trim();
        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data != null)
        {
            var guid = data.UserId;
            var whitelists = await _db.GetJobWhitelists(guid);
            if (whitelists.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-jobwhitelistget-whitelisted-none", ("player", player)));
                return;
            }

            shell.WriteLine(Loc.GetString("cmd-jobwhitelistget-whitelisted-for",
                ("player", player),
                ("jobs", string.Join(", ", whitelists))));
            return;
        }

        shell.WriteError(Loc.GetString("cmd-jobwhitelist-player-not-found", ("player", player)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-jobwhitelist-hint-player"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed partial class RemoveJobWhitelistCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "jobwhitelistremove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var player = args[0].Trim();
        var roleId = args[1].Trim();

        string roleName;
        if (_prototypes.TryIndex<JobPrototype>(roleId, out var jobPrototype))
        {
            roleName = jobPrototype.LocalizedName;
        }
        else if (_prototypes.TryIndex<AntagPrototype>(roleId, out var antagPrototype))
        {
            roleName = Loc.GetString(antagPrototype.Name);
        }
        else
        {
            shell.WriteError(Loc.GetString("cmd-jobwhitelist-job-does-not-exist", ("job", roleId)));
            shell.WriteLine(Help);
            return;
        }

        var data = await _playerLocator.LookupIdByNameAsync(player);
        if (data == null)
        {
            shell.WriteError(Loc.GetString("cmd-jobwhitelist-player-not-found", ("player", player)));
            return;
        }

        var guid = data.UserId;
        if (!await _db.IsRoleWhitelisted(guid, roleId))
        {
            shell.WriteError(Loc.GetString("cmd-jobwhitelistremove-was-not-whitelisted",
                ("player", player),
                ("jobId", roleId),
                ("jobName", roleName)));
            return;
        }

        _jobWhitelist.RemoveWhitelist(guid, roleId);
        shell.WriteLine(Loc.GetString("cmd-jobwhitelistremove-removed",
            ("player", player),
            ("jobId", roleId),
            ("jobName", roleName)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                _players.Sessions.Select(s => s.Name),
                Loc.GetString("cmd-jobwhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            var options = _prototypes.EnumeratePrototypes<JobPrototype>().Select(p => p.ID)
                .Concat(_prototypes.EnumeratePrototypes<AntagPrototype>().Select(p => p.ID));
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-jobwhitelist-hint-job"));
        }

        return CompletionResult.Empty;
    }
}
