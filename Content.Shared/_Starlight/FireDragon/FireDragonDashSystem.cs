using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.FireDragon;

/// <summary>
/// Drives a short forward dash through the normal collision solver.
/// Blocking input movement prevents the mover from overwriting its velocity.
/// </summary>
public sealed partial class FireDragonDashSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<FireDragonDashComponent> ent, ref ComponentStartup args) =>
        _blocker.UpdateCanMove(ent);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FireDragonDashComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.EndTime = TimeSpan.Zero;
        if (!TerminatingOrDeleted(ent))
            _blocker.UpdateCanMove(ent);
    }

    [SubscribeLocalEvent]
    private void OnCanMove(Entity<FireDragonDashComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.EndTime > _timing.CurTime)
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnCollide(Entity<FireDragonDashComponent> ent, ref StartCollideEvent args)
    {
        if (!_net.IsServer
            || !args.OtherFixture.Hard
            || !TryComp<PhysicsComponent>(args.OtherEntity, out var other)
            || other.BodyType != BodyType.Static)
            return;

        EndDash(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<FireDragonDashComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dash, out var body))
        {
            if (dash.EndTime <= _timing.CurTime)
            {
                EndDash((uid, dash));
                continue;
            }

            _physics.SetLinearVelocity(uid, dash.Direction * dash.Speed, body: body);
        }
    }

    private void EndDash(Entity<FireDragonDashComponent> ent)
    {
        ent.Comp.EndTime = TimeSpan.Zero;
        Dirty(ent);
        if (TryComp<PhysicsComponent>(ent, out var body))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: body);

        _blocker.UpdateCanMove(ent);
        RemCompDeferred<FireDragonDashComponent>(ent);
    }
}
