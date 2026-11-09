using System.Linq;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Disposal.Unit;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TransitPodComponent, DisposalHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pod, out var holder, out var xform))
        {
            var ent = new Entity<TransitPodComponent>(uid, pod);

            if (pod.State == TransitPodState.Stalled || (pod.State == TransitPodState.Moving && holder.Halted))
                CheckSupport(ent, (uid, holder), xform);

            switch (pod.State)
            {
                case TransitPodState.Moving:
                    UpdateHeld(ent, holder, now);
                    CheckCollisions(ent, holder, xform, now, frameTime);
                    break;

                case TransitPodState.Opening when now >= pod.StateEnd:
                    if (TryGetStation(pod, out var opening))
                        OpenPod(ent, opening);
                    break;

                case TransitPodState.Open:
                    UpdateOpen(ent, now);
                    break;

                case TransitPodState.Closing when now >= pod.StateEnd:
                    Depart(ent, (uid, holder));
                    break;
            }

            if (now < pod.NextAtmosUpdate || TerminatingOrDeleted(uid))
                continue;

            pod.NextAtmosUpdate = now + _atmosInterval;

            if (pod.State == TransitPodState.Open)
                ExchangeAir(holder, (uid, xform));
            else
                ExchangeHeat(ent, holder, xform, (float)_atmosInterval.TotalSeconds);
        }
    }

    private void CheckSupport(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, TransformComponent xform)
    {
        var current = holder.Comp.CurrentTube;
        if (TryGetTube(xform, out _) && (current == null || Exists(current)))
            return;

        if (current == null && !GetPassengers(pod).Any())
            return;

        _holder.ClearTubes(holder.AsNullable());
        if (pod.Comp.State == TransitPodState.Moving)
            Stall(pod, holder);
        else
            EjectAll(pod, xform.Coordinates);
    }

    private void UpdateOpen(Entity<TransitPodComponent> pod, TimeSpan now)
    {
        // Pods keep to their timetable and leave whether or not anyone got in.
        if (now < pod.Comp.NextDeparture || !TryGetStation(pod.Comp, out var station))
            return;

        // Held by a signal: nothing leaves until Depart fires, wherever it is headed.
        if (station.Comp.Holding)
        {
            pod.Comp.NextDeparture = now + station.Comp.DepartDelay;
            return;
        }

        // Parked at a depot until someone calls it.
        if (station.Comp.Depot && pod.Comp.Target == null && !pod.Comp.AnyFree)
        {
            pod.Comp.NextDeparture = now + station.Comp.DepartDelay;
            return;
        }

        // Looking for any free station and none is: keep the doors open and look again later.
        if ((pod.Comp.AnyFree || pod.Comp.Target != null) && !TryPlanTrip(pod, station) && pod.Comp.AnyFree)
        {
            pod.Comp.NextDeparture = now + station.Comp.DepartDelay;
            return;
        }

        SetState(pod, TransitPodState.Closing, station.Comp.CloseTime);
        SetStationVisual(station, TransitStationVisualState.Closing);
        _audio.PlayPvs(station.Comp.CloseSound, station);
    }

    private void CheckCollisions(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TransformComponent xform, TimeSpan now, float frameTime)
    {
        if (now < pod.Comp.NextCollision || !TryComp<PhysicsComponent>(pod, out var physics))
            return;

        // Fast pods cover a lot of tube per tick; widen the check so they can't skip through each other.
        var range = pod.Comp.CollisionRange + ((2f * holder.TraversalSpeed) * frameTime);

        _nearbyPods.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, range, _nearbyPods);

        var position = _xform.GetWorldPosition(xform);
        foreach (var other in _nearbyPods)
        {
            if (other.Owner == pod.Owner || now < other.Comp.NextCollision)
                continue;

            // Pods on separate tubes side by side pass each other.
            if (!PathsMeet(holder, xform, other))
                continue;

            var offset = _xform.GetWorldPosition(other) - position;
            var otherVelocity = TryComp<PhysicsComponent>(other, out var otherPhysics) && other.Comp.State == TransitPodState.Moving
                ? otherPhysics.LinearVelocity
                : System.Numerics.Vector2.Zero;

            // Only pods closing in on each other crash; ones pulling apart are fine.
            if (System.Numerics.Vector2.Dot(otherVelocity - physics.LinearVelocity, offset) >= 0)
                continue;

            // A pod standing on the line is waited for, unless this one is going too fast to stop.
            if (!pod.Comp.Boosted && !holder.Halted && StandsInTheWay(other))
            {
                // Waiting here would make a loop of pods waiting on each other: turn back instead.
                if (WaitWouldCycle(pod, other))
                {
                    TurnBack(pod, (pod.Owner, holder), now);
                    return;
                }

                pod.Comp.WaitingBehind = other;
                Hold(pod, (pod.Owner, holder));
                continue;
            }

            // Boosted pods hit much harder, both for themselves and whoever they hit.
            var multiplier = pod.Comp.Boosted || other.Comp.Boosted ? pod.Comp.BoostedCollisionMultiplier : 1f;

            Crash(pod, (pod.Owner, holder), now, multiplier);

            if (other.Comp.State == TransitPodState.Moving && TryComp<DisposalHolderComponent>(other, out var otherHolder))
                Crash(other, (other.Owner, otherHolder), now, multiplier);
            else
                HurtPassengers(other, other.Comp.CollisionDamage, multiplier);

            _audio.PlayPvs(pod.Comp.CollisionSound, pod);
            return;
        }
    }

    private bool PathsMeet(DisposalHolderComponent holder, TransformComponent xform, Entity<TransitPodComponent> other)
    {
        _pathTubes.Clear();
        if (holder.CurrentTube is { } current)
            _pathTubes.Add(current);
        if (holder.NextTube is { } next)
            _pathTubes.Add(next);
        if (TryGetTube(xform, out var under))
            _pathTubes.Add(under);

        if (TryComp<DisposalHolderComponent>(other, out var otherHolder))
        {
            if ((otherHolder.CurrentTube is { } otherCurrent && _pathTubes.Contains(otherCurrent))
                || (otherHolder.NextTube is { } otherNext && _pathTubes.Contains(otherNext)))
                return true;
        }

        return TryGetTube(Transform(other), out var otherUnder) && _pathTubes.Contains(otherUnder);
    }

    private void Crash(Entity<TransitPodComponent> pod, Entity<DisposalHolderComponent> holder, TimeSpan now, float multiplier)
    {
        pod.Comp.NextCollision = now + _collisionCooldown;
        HurtPassengers(pod, pod.Comp.CollisionDamage, multiplier);

        // A held pod gets shoved back into motion. Its plan led the other way: the trip is off,
        // so it stops at the next station it can rather than wandering past them.
        ReleaseHold(pod, holder);
        if (pod.Comp.Target != null || pod.Comp.AnyFree)
        {
            CancelTrip(pod);
            PopupPassengers(pod, "transit-pod-trip-cancelled");
        }

        ReleaseReservation(pod);

        if (!_holder.Reverse(holder.AsNullable()))
            Stall(pod, holder);
    }

    private void HurtPassengers(Entity<TransitPodComponent> pod, DamageSpecifier damage, float multiplier)
    {
        // A snapshot: a passenger gibbed by the hit can drop things into the pod.
        foreach (var passenger in GetPassengers(pod).ToArray())
        {
            if (TerminatingOrDeleted(passenger))
                continue;

            _damageable.ChangeDamage(passenger, damage * multiplier);
            _stun.TryUpdateParalyzeDuration(passenger, pod.Comp.CollisionStun * multiplier);
            _popup.PopupEntity(Loc.GetString("transit-pod-crash"), passenger, passenger, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// An open pod shares its air with the room it is docked in.
    /// </summary>
    private void ExchangeAir(DisposalHolderComponent holder, Entity<TransformComponent> xform)
    {
        // Read into a local: the holder only grants read access to its members.
        var air = holder.Air;
        if (_atmos.GetTileMixture((xform.Owner, xform.Comp), excite: true) is not { } env)
        {
            air.Clear();
            return;
        }

        _atmos.Merge(env, air);
        air.Clear();
        _atmos.Merge(air, env.RemoveVolume(air.Volume));
    }

    /// <summary>
    /// A closed pod keeps its air, but its walls pass heat from whatever it rides through.
    /// </summary>
    private void ExchangeHeat(Entity<TransitPodComponent> pod, DisposalHolderComponent holder, TransformComponent xform, float dt)
    {
        var air = holder.Air;
        // Even a pod that passes no heat checks for fire: hot air from the last station burns all the same.
        if (air.TotalMoles <= 0)
            return;

        var target = air.Temperature;
        var rate = 0f;

        // Convection scales with how dense the outside gas is, so vacuum barely cools the pod.
        if (_atmos.GetTileMixture((pod.Owner, xform)) is { } env && env.TotalMoles > 0)
        {
            target = env.Temperature;
            rate = pod.Comp.HeatConductivity * Math.Clamp(env.Pressure / Atmospherics.OneAtmosphere, 0f, 1f);
        }

        // Lava and the like heat the pod by radiation, gas or not.
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var anchored = _map.GetAnchoredEntities(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
            while (anchored.MoveNext(out var anchoredUid))
            {
                if (!TryComp<RadiantHeatComponent>(anchoredUid, out var heat) || heat.Temperature <= target)
                    continue;

                target = heat.Temperature;
                rate = pod.Comp.HeatConductivity;
            }
        }

        // The tube's own glass keeps some of it off.
        rate *= 1f - GetHeatInsulation(xform);

        // Still check for fire below: hot air from the last station burns even in vacuum.
        if (rate > 0)
            air.Temperature += (target - air.Temperature) * (1f - MathF.Exp(-rate * dt));

        if (air.Temperature < pod.Comp.IgnitionTemperature)
            return;

        foreach (var passenger in GetPassengers(pod).ToArray())
        {
            _flammable.AdjustFireStacks(passenger, pod.Comp.IgnitionFireStacks, ignite: true);
        }
    }
}
