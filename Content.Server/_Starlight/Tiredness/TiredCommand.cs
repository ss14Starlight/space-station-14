using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Starlight.Tiredness.Components;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Tiredness;

[AdminCommand(AdminFlags.Debug)]
public sealed class TiredCommand : LocalizedEntityCommands
{
    public override string Command => "tired";

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

        if (!EntityManager.TryGetComponent(playerEntity, out TirednessComponent? tiredness))
        {
            shell.WriteError(Loc.GetString("cmd-tiredness-error-component", ("comp", nameof(TirednessComponent))));
            return;
        }

        EntityManager.System<TirednessSystem>().SetTiredness((playerEntity, tiredness), TirednessSystem.MaximumTiredness);
    }
}
