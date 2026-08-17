using System.Linq;
using Content.Server.Administration;  // AdminCommandAttribute + IPlayerLocator
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Administration;  // AdminFlags
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Inferus.Administration.Commands;

/// <summary>
/// Admin commands to grant/revoke job or antag roles, bypassing playtime requirements.
/// Uses the existing job-whitelist DB + net sync; pair with the IsAllowed patches in PATCHES/.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed class GrantRoleCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "grantrole";

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

        if (await _db.IsRoleWhitelisted(guid, roleId))
        {
            shell.WriteLine(Loc.GetString("cmd-grantrole-already-granted",
                ("player", player),
                ("roleId", roleId),
                ("roleName", roleName)));
            return;
        }

        _jobWhitelist.AddWhitelist(guid, roleId);
        shell.WriteLine(Loc.GetString("cmd-grantrole-granted",
            ("player", player),
            ("roleId", roleId),
            ("roleName", roleName)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(),
                Loc.GetString("cmd-jobwhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            var options = CompletionHelper.PrototypeIDs<JobPrototype>()
                .Concat(CompletionHelper.PrototypeIDs<AntagPrototype>());
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-jobwhitelist-hint-job"));
        }

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Ban)]
public sealed class RevokeRoleCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override string Command => "revokerole";

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
            shell.WriteError(Loc.GetString("cmd-revokerole-not-granted",
                ("player", player),
                ("roleId", roleId),
                ("roleName", roleName)));
            return;
        }

        _jobWhitelist.RemoveWhitelist(guid, roleId);
        shell.WriteLine(Loc.GetString("cmd-revokerole-revoked",
            ("player", player),
            ("roleId", roleId),
            ("roleName", roleName)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(),
                Loc.GetString("cmd-jobwhitelist-hint-player"));
        }

        if (args.Length == 2)
        {
            var options = CompletionHelper.PrototypeIDs<JobPrototype>()
                .Concat(CompletionHelper.PrototypeIDs<AntagPrototype>());
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-jobwhitelist-hint-job"));
        }

        return CompletionResult.Empty;
    }
}
