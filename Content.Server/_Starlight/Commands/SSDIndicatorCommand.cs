using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.SSDIndicator;
using Robust.Shared.Console;
using Content.Shared.Starlight.CryoTeleportation;

namespace Content.Shared._Starlight.Commands.SSDIndicator;

[AnyCommand]
public sealed class SSDIndicatorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "ssd";
    public string Description => Loc.GetString("ssd-indicator-command-description");
    public string Help => Loc.GetString("ssd-indicator-command-help-text");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;

        if (player == null)
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-no-character"));
            return;
        }

        var gameTicker = _entities.System<GameTicker>();
        if (!gameTicker.PlayerGameStatuses.TryGetValue(player.UserId, out var playerStatus) ||
            playerStatus != PlayerGameStatus.JoinedGame)
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-no-character"));
            return;
        }

        if (player.AttachedEntity is { Valid: true } frozen &&
            _entities.HasComponent<AdminFrozenComponent>(frozen))
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-denied"));
            return;
        }

        if (!_entities.TryGetComponent(player.AttachedEntity, out SSDIndicatorComponent? indicatorComponent))
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-no-character"));
            return;
        }

        if (indicatorComponent.IsSSD)
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-denied"));
            return;
        }
        if (!_entities.System<SSDIndicatorSystem>().TrySSD((EntityUid)player.AttachedEntity!, indicatorComponent))
        {
            shell.WriteLine(Loc.GetString("ssd-indicator-command-denied"));
            return;
        }

        if (_entities.TryGetComponent(player.AttachedEntity, out TargetCryoTeleportationComponent? cryoTeleport))
            cryoTeleport.TimeDelay = TimeSpan.FromMinutes(10); // Delay cryo teleportation for 10 minutes.
    }
}
