using Content.Server.Administration;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Preferences;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class CharacterForcePrototypeCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    public override string Command => "characterforceprototype";
    public override string Description => "Changed ForcedPrototype on the character slot selected.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        ICommonSession? player;
        if (args.Length > 0)
            _players.TryGetSessionByUsername(args[0], out player);
        else
            player = shell.Player;

        if (player == null)
        {
            shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
            return;
        }

        var selectedproto = "";
        if (!args[2].Equals("null"))
        {
            if (!_prototypeManager.Resolve(args[2], out _))
            {
                shell.WriteError(Loc.GetString("cmd-tippy-error-no-prototype", ("proto", args[2])));
                return;
            }
            selectedproto = args[2];
        }

        var profile = _prefsManager.GetPreferences(player.UserId).Characters[int.Parse(args[1])] as HumanoidCharacterProfile;
        _prefsManager.SetProfile(player.UserId, int.Parse(args[1]), profile!.WithForcedPrototype(selectedproto));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), LocalizationManager.GetString("shell-argument-username-hint"));
        }

        return CompletionResult.Empty;
    }
}