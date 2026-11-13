using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Tiredness;

[AdminCommand(AdminFlags.Debug)]
public sealed class SetTirednessCommand : LocalizedEntityCommands
{
    public override string Command => "settiredness";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteError(Loc.GetString("cmd-tiredness-error-player"));
            return;
        }

        if (player.AttachedEntity is not { Valid: true } playerEntity)
        {
            shell.WriteError(Loc.GetString("cmd-tiredness-error-entity"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 1),
                ("currentAmount", args.Length)
            ));
            return;
        }

        if (!EntityManager.TryGetComponent(playerEntity, out TirednessComponent? tiredness))
        {
            shell.WriteError(Loc.GetString("cmd-tiredness-error-component", ("comp", nameof(TirednessComponent))));
            return;
        }

        if (!double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) ||
            !double.IsFinite(minutes) ||
            minutes < 0 ||
            minutes > TirednessSystem.MaximumTiredness.TotalMinutes)
        {
            shell.WriteError(Loc.GetString("cmd-settiredness-error-invalid-value",
                ("value", args[0]),
                ("maximum", TirednessSystem.MaximumTiredness.TotalMinutes)
            ));
            return;
        }

        EntityManager.System<TirednessSystem>().SetTiredness((playerEntity, tiredness), TimeSpan.FromMinutes(minutes));
    }
}
