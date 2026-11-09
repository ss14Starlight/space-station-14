using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Light.NightLighting;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Light.NightShift;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NightShiftCommand : LocalizedEntityCommands
{
    [Dependency] private NightShiftSystem _nightShift = default!;
    [Dependency] private StationSystem _station = default!;

    public override string Command => "nightshift";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => args.Length switch
        {
            1 => CompletionResult.FromHintOptions(Enum.GetNames<ApcLightingMode>(), Loc.GetString("cmd-nightshift-hint-mode")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Components<StationNightShiftComponent>(args[1], EntityManager),
                Loc.GetString("cmd-nightshift-hint-station")),
            _ => CompletionResult.Empty,
        };

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!Enum.TryParse<ApcLightingMode>(args[0], true, out var mode) || !Enum.IsDefined(mode))
        {
            shell.WriteError(Loc.GetString("cmd-nightshift-invalid-mode", ("mode", args[0])));
            return;
        }

        if (args.Length == 1)
        {
            foreach (var station in _station.GetStations())
            {
                _nightShift.SetOverride(station, mode);
            }

            shell.WriteLine(Loc.GetString("cmd-nightshift-success-all", ("mode", mode)));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var netEntity)
            || !EntityManager.TryGetEntity(netEntity, out var target)
            || !_station.GetStations().Contains(target.Value))
        {
            shell.WriteError(Loc.GetString("cmd-nightshift-invalid-station", ("station", args[1])));
            return;
        }

        _nightShift.SetOverride(target.Value, mode);
        shell.WriteLine(Loc.GetString("cmd-nightshift-success", ("mode", mode), ("station", EntityManager.ToPrettyString(target.Value))));
    }
}
