using Content.Shared.Input;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client.Shuttles.Systems
{
    public sealed class ShuttleConsoleSystem : SharedShuttleConsoleSystem
    {
        [Dependency] private readonly IInputManager _input = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PilotComponent, ComponentHandleState>(OnHandleState);
            // Starlight: reset input if the console entity itself is destroyed while we're still piloting.
            SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
            var shuttle = _input.Contexts.New("shuttle", "common");
            shuttle.AddFunction(ContentKeyFunctions.ShuttleStrafeUp);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleStrafeDown);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleStrafeLeft);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleStrafeRight);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleRotateLeft);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleRotateRight);
            shuttle.AddFunction(ContentKeyFunctions.ShuttleBrake);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _input.Contexts.Remove("shuttle");
        }

        // Starlight: safety net — if the console entity is deleted while the local player is still
        // piloting it, RemovePilot may have bailed early server-side (console component already gone),
        // leaving PilotComponent on the PAI. Reset input here so the screen never stays frozen.
        private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
        {
            var localEntity = _playerManager.LocalEntity;
            if (localEntity == null)
                return;
            if (!TryComp<PilotComponent>(localEntity.Value, out var pilot))
                return;
            if (pilot.Console != uid)
                return;

            _input.Contexts.SetActiveContext("human");
        }

        protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
        {
            base.HandlePilotShutdown(uid, component, args);
            if (_playerManager.LocalEntity != uid) return;

            _input.Contexts.SetActiveContext("human");
        }

        private void OnHandleState(EntityUid uid, PilotComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not PilotComponentState state) return;

            var console = EnsureEntity<PilotComponent>(state.Console, uid);

            if (console == null)
            {
                component.Console = null;
                _input.Contexts.SetActiveContext("human");
                return;
            }

            if (!HasComp<ShuttleConsoleComponent>(console))
            {
                Log.Warning($"Unable to set Helmsman console to {console}");
                return;
            }

            component.Console = console;
            ActionBlockerSystem.UpdateCanMove(uid);
            _input.Contexts.SetActiveContext("shuttle");
        }
    }
}
