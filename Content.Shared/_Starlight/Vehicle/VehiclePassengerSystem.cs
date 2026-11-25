using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Vehicle;

public sealed partial class VehiclePassengerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnInsertAttempt(Entity<VehiclePassengerSeatsComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (_timing.ApplyingState || !_vehicles.TryGetOperatorContainer(ent.Owner, out var seats) ||
            seats != args.Container)
            return;

        if (seats.ContainedEntities.Count >= ent.Comp.Seats ||
            CompOrNull<VehicleHullComponent>(ent)?.Broken == true ||
            _whitelist.IsWhitelistFail(ent.Comp.PassengerWhitelist, args.EntityUid))
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnPassengerAttack(Entity<BodyComponent> ent, ref CanAttackFromContainerEvent args)
    {
        if (_vehicles.GetOccupiedVehicle(ent.Owner) is { } vehicle && args.Target != vehicle &&
            HasComp<VehiclePassengerSeatsComponent>(vehicle) &&
            TryComp<VehicleComponent>(vehicle, out var comp) && comp.CanAttack)
            args.CanAttack = true;
    }

    [SubscribeLocalEvent]
    private void OnPassengerVerbs(EntityUid uid, VehiclePassengerSeatsComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        var ent = new Entity<VehiclePassengerSeatsComponent>(uid, component);
        if (!args.CanAccess || !args.CanInteract || !_vehicles.TryGetOperatorContainer(ent.Owner, out var seats))
            return;

        var user = args.User;
        var driver = _vehicles.GetOperatorOrNull(ent.Owner);
        if (seats.Contains(user))
        {
            if (driver != user)
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("vehicle-verb-leave"),
                    Act = () => _containers.Remove(user, seats),
                });
            }

            return;
        }

        foreach (var passenger in seats.ContainedEntities)
        {
            if (passenger == driver)
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Category = VerbCategory.Eject,
                IconEntity = GetNetEntity(passenger),
                Text = Loc.GetString("vehicle-verb-remove", ("passenger", Identity.Entity(passenger, EntityManager))),
                Act = () =>
                {
                    if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.RemoveTime,
                            new RemoveVehiclePassengerDoAfter(GetNetEntity(passenger)), ent.Owner, target: ent.Owner)
                        { BreakOnMove = true, BreakOnDamage = true }))
                    {
                        _popup.PopupEntity(Loc.GetString("vehicle-remove-passenger-attempt",
                            ("user", Identity.Entity(user, EntityManager))), ent.Owner, passenger, PopupType.LargeCaution);
                    }
                },
            });
        }
    }

    [SubscribeLocalEvent]
    private void OnPassengerRemoved(EntityUid owner, VehiclePassengerSeatsComponent component, RemoveVehiclePassengerDoAfter args)
    {
        var ent = new Entity<VehiclePassengerSeatsComponent>(owner, component);
        if (args.Cancelled || args.Handled || !_interaction.InRangeUnobstructed(args.User, ent.Owner) ||
            !_vehicles.TryGetOperatorContainer(ent.Owner, out var seats) ||
            !TryGetEntity(args.Passenger, out var passenger) || passenger is not { } uid || !seats.Contains(uid) ||
            uid == _vehicles.GetOperatorOrNull(ent.Owner))
            return;

        args.Handled = _containers.Remove(uid, seats);
    }

    [SubscribeLocalEvent]
    private void OnBreakage(EntityUid uid, VehiclePassengerSeatsComponent component, BreakageEventArgs args)
    {
        if (_vehicles.TryGetOperatorContainer(uid, out var seats))
            _containers.EmptyContainer(seats, force: true);
    }

    [SubscribeLocalEvent]
    private void OnDamage(EntityUid owner, VehiclePassengerSeatsComponent component, DamageChangedEvent args)
    {
        var ent = new Entity<VehiclePassengerSeatsComponent>(owner, component);
        if (!args.DamageIncreased || args.DamageDelta is not { } damage || args.Origin == ent.Owner ||
            !_vehicles.TryGetOperatorContainer(ent.Owner, out var seats) || seats.ContainedEntities.Count == 0)
            return;

        var share = DamageSpecifier.GetPositive(damage) * (ent.Comp.DamageTransfer / seats.ContainedEntities.Count);
        foreach (var passenger in seats.ContainedEntities.ToArray())
        {
            if (!TerminatingOrDeleted(passenger))
                _damageable.TryChangeDamage(passenger, share, origin: args.Origin);
        }
    }
}
