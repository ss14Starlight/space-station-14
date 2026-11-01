using Content.Server.Ghost;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class BodySystem : SharedBodySystem
{
    [Dependency] private GhostSystem _ghostSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;

    [SubscribeLocalEvent]
    private void OnRelayMoveInput(Entity<SLBodyComponent> ent, ref MoveInputEvent args)
    {
        // If they haven't actually moved then ignore it.
        if ((args.Entity.Comp.HeldMoveButtons &
             (MoveButtons.Down | MoveButtons.Left | MoveButtons.Up | MoveButtons.Right)) == 0x0)
            return;

        if (_mobState.IsDead(ent) && _mindSystem.TryGetMind(ent, out var mindId, out var mind))
        {
            mind.TimeOfDeath ??= _gameTiming.RealTime;
            _ghostSystem.OnGhostAttempt(mindId, canReturnGlobal: true, mind: mind);
        }
    }

    [SubscribeLocalEvent]
    private void OnApplyMetabolicMultiplier(Entity<SLBodyComponent> ent, ref ApplyMetabolicMultiplierEvent args)
    {
        foreach (var organ in GetBodyOrgans(ent, ent))
        {
            RaiseLocalEvent(organ.Id, ref args);
        }
    }
}
