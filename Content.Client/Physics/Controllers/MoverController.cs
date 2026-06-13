using Content.Shared.Alert;
using Content.Shared.CCVar;
using Content.Shared.Friction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Starlight.CCVar;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.PhysicsSystem.Controllers;

public sealed class MoverController : SharedMoverController
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Starlight start
    // same gating as the server, otherwise we predict per-substep, server does it per-tick and we
    // rubber-band. replicated cvar so we just follow whatever the server's set to.
    private bool _substepGating;
    private GameTick _lastUpdateTick;
    // who was active on the first substep. UpdateAfterSolve clears the used-set so we re-stamp it on
    // coasted substeps, else the client friction controller stops skipping our movers.
    private readonly List<EntityUid> _predictedUsed = new();
    // Starlight end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RelayInputMoverComponent, LocalPlayerAttachedEvent>(OnRelayPlayerAttached);
        SubscribeLocalEvent<RelayInputMoverComponent, LocalPlayerDetachedEvent>(OnRelayPlayerDetached);
        SubscribeLocalEvent<InputMoverComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<InputMoverComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<InputMoverComponent, UpdateIsPredictedEvent>(OnUpdatePredicted);
        SubscribeLocalEvent<MovementRelayTargetComponent, UpdateIsPredictedEvent>(OnUpdateRelayTargetPredicted);
        SubscribeLocalEvent<PullableComponent, UpdateIsPredictedEvent>(OnUpdatePullablePredicted);

        // Starlight start
        Subs.CVar(_cfg, StarlightCCVars.PhysicsMoverSubstepGating, value => _substepGating = value, true);
        // Starlight end
    }

    private void OnUpdatePredicted(Entity<InputMoverComponent> entity, ref UpdateIsPredictedEvent args)
    {
        // Enable prediction if an entity is controlled by the player
        if (entity.Owner == _playerManager.LocalEntity)
            args.IsPredicted = true;
    }

    private void OnUpdateRelayTargetPredicted(Entity<MovementRelayTargetComponent> entity, ref UpdateIsPredictedEvent args)
    {
        if (entity.Comp.Source == _playerManager.LocalEntity)
            args.IsPredicted = true;
    }

    private void OnUpdatePullablePredicted(Entity<PullableComponent> entity, ref UpdateIsPredictedEvent args)
    {
        // Enable prediction if an entity is being pulled by the player.
        // Disable prediction if an entity is being pulled by some non-player entity.

        if (entity.Comp.Puller == _playerManager.LocalEntity)
            args.IsPredicted = true;
        else if (entity.Comp.Puller != null)
            args.BlockPrediction = true;

        // TODO recursive pulling checks?
        // What if the entity is being pulled by a vehicle controlled by the player?
    }

    private void OnRelayPlayerAttached(Entity<RelayInputMoverComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        PhysicsSystem.UpdateIsPredicted(entity.Owner);
        PhysicsSystem.UpdateIsPredicted(entity.Comp.RelayEntity);
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnRelayPlayerDetached(Entity<RelayInputMoverComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        PhysicsSystem.UpdateIsPredicted(entity.Owner);
        PhysicsSystem.UpdateIsPredicted(entity.Comp.RelayEntity);
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnPlayerAttached(Entity<InputMoverComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
    }

    private void OnPlayerDetached(Entity<InputMoverComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (_playerManager.LocalEntity is not {Valid: true} player)
            return;

        // Starlight start
        // same deal as the server: predict velocity once per tick (over the full tick) then coast the
        // rest of the substeps, just putting the used-set back so friction keeps skipping our movers.
        if (_substepGating)
        {
            if (_timing.CurTick == _lastUpdateTick)
            {
                for (var i = 0; i < _predictedUsed.Count; i++)
                    UsedMobMovement[_predictedUsed[i]] = true;
                return;
            }
            _lastUpdateTick = _timing.CurTick;
        }

        var moverFrameTime = _substepGating ? (float)_timing.TickPeriod.TotalSeconds : frameTime;

        _predictedUsed.Clear();

        if (RelayQuery.TryGetComponent(player, out var relayMover))
        {
            HandleClientsideMovement(relayMover.RelayEntity, moverFrameTime);
            if (_substepGating && UsedMobMovement.TryGetValue(relayMover.RelayEntity, out var relayUsed) && relayUsed)
                _predictedUsed.Add(relayMover.RelayEntity);
        }

        HandleClientsideMovement(player, moverFrameTime);
        if (_substepGating && UsedMobMovement.TryGetValue(player, out var playerUsed) && playerUsed)
            _predictedUsed.Add(player);
        // Starlight end
    }

    private void HandleClientsideMovement(EntityUid player, float frameTime)
    {
        if (!MoverQuery.TryGetComponent(player, out var mover))
        {
            return;
        }

        // Server-side should just be handled on its own so we'll just do this shizznit
        HandleMobMovement((player, mover), frameTime);
    }

    protected override bool CanSound()
    {
        return _timing is { IsFirstTimePredicted: true, InSimulation: true };
    }

    public override void SetSprinting(Entity<InputMoverComponent> entity, ushort subTick, bool walking)
    {
        // Logger.Info($"[{_gameTiming.CurTick}/{subTick}] Sprint: {enabled}");
        base.SetSprinting(entity, subTick, walking);

        if (walking && _cfg.GetCVar(CCVars.ToggleWalk))
            _alerts.ShowAlert(entity.Owner, WalkingAlert, showCooldown: false, autoRemove: false);
        else
            _alerts.ClearAlert(entity.Owner, WalkingAlert);
    }
}
