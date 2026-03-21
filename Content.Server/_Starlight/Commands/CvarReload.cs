using System.IO;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class ReloadConfigCommand : LocalizedCommands
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    public override string Command => "reloadconfig";
    public override string Description => "Reloading cvars from disk. Attention required Starlight watchdog or cvar setup config.file.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 0)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }
        var path = _cfg.GetCVar(StarlightCCVars.ConfigFile);
        if (string.IsNullOrEmpty(path))
        {
            shell.WriteLine("Config file is not set.");
            return;
        }

        if (!File.Exists(path))
        {
            shell.WriteLine("Config file does not exist.");
            return;
        }

        using var file = File.OpenRead(path);
        _cfg.LoadFromTomlStream(file);
        shell.WriteLine("Config reloaded.");
    }
}