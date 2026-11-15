using Content.Server._Starlight.NewLife;
using Content.Server._Starlight.Station.Systems;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking.Events;
using Robust.Shared.Player;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Commands
{
    [AnyCommand]
    sealed partial class JoinGameCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _entManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private ILogManager _logManager = default!;
        #region Starlight
        [Dependency] private IServerPreferencesManager _preferencesManager = default!;
        [Dependency] private IBanManager _banManager = default!;
        #endregion

        private readonly ISawmill _sawmill;

        public string Command => "joingame";
        public string Description => "";
        public string Help => "";

        public JoinGameCommand()
        {
            IoCManager.InjectDependencies(this);

            _sawmill = _logManager.GetSawmill("security");
        }

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 3)
            {
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            var player = shell.Player;

            if (player == null)
            {
                return;
            }

            var ticker = _entManager.System<GameTicker>();
            var stationJobs = _entManager.System<StationJobsSystem>();

            if (ticker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                shell.WriteLine("Round has not started.");
                return;
            }
            else if (ticker.RunLevel == GameRunLevel.InRound)
            {
                if (!int.TryParse(args[0], out var charSlot))
                {
                    shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
                    return; // Starlight-edit
                }
                string id = args[1];

                if (!int.TryParse(args[2], out var sid))
                {
                    shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
                    return; // Starlight-edit
                }

                if (ticker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) && status == PlayerGameStatus.JoinedGame)
                {
                    // Starlight-start
                    var newLifeSystem = _entManager.System<NewLifeSystem>();

                    if (!newLifeSystem.CanTakeNewLife(player, charSlot, out var reason))
                    {
                        _sawmill.Info($"{player.Name} ({player.UserId}) attempted to latejoin while in-game ({reason}).");
                        shell.WriteError($"{player.Name} is not in the lobby.   This incident will be reported.");
                        return;
                    }
                    // Starlight-end
                }

                // Starlight-start: validate the client-supplied job and station server-side
                if (!_prototypeManager.TryIndex<JobPrototype>(id, out var jobPrototype))
                {
                    shell.WriteError(Loc.GetString("join-game-command-unknown-job"));
                    return;
                }

                var station = _entManager.GetEntity(new NetEntity(sid));
                if (!_entManager.HasComponent<StationJobsComponent>(station))
                {
                    shell.WriteError(Loc.GetString("join-game-command-invalid-station"));
                    return;
                }

                if (!IsJobAllowed(player, jobPrototype.ID))
                {
                    _sawmill.Info($"{player.Name} ({player.UserId}) attempted to latejoin as restricted job {jobPrototype.ID}.");
                    shell.WriteError(Loc.GetString("join-game-command-job-restricted", ("job", jobPrototype.LocalizedName)));
                    return;
                }

                _entManager.System<ContainerSpawnJobSlotSystem>().RefreshJobSlots();
                // Starlight-end
                if (stationJobs.TryGetJobSlot(station, jobPrototype, out var slots) == false || slots == 0)
                {
                    shell.WriteLine($"{jobPrototype.LocalizedName} has no available slots.");
                    return;
                }

                if (!_preferencesManager.GetPreferences(player.UserId).TryGetHumanoidInSlot(charSlot, out var humanoid))
                {
                    shell.WriteLine("No profile in slot");
                    return;
                }

                // Starlight-start: species, trait and other profile requirements of the job, checked on the slot being spawned.
                if (!_entManager.System<PlayTimeTrackingSystem>().IsProfileAllowedForJob(player, jobPrototype.ID, humanoid))
                {
                    _sawmill.Info($"{player.Name} ({player.UserId}) attempted to latejoin as {jobPrototype.ID} with a profile that fails its requirements.");
                    shell.WriteError(Loc.GetString("join-game-command-profile-restricted", ("job", jobPrototype.LocalizedName)));
                    return;
                }
                // Starlight-end

                if (_adminManager.IsAdmin(player) && _cfg.GetCVar(CCVars.AdminDeadminOnJoin))
                {
                    _adminManager.DeAdmin(player);
                }

                ticker.MakeJoinGame(player, humanoid, station, id);
                return;
            }

            ticker.MakeJoinGame(player, EntityUid.Invalid);
        }
    }
}
