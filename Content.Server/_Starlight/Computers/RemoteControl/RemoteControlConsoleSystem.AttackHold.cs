using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    private static readonly TimeSpan RemoteAttackHoldTimeout = TimeSpan.FromSeconds(1);

    private readonly Dictionary<(EntityUid Console, bool AltInteract), RemoteAttackHoldState>
        _remoteAttackHolds = new();

    private readonly List<(EntityUid Console, bool AltInteract)> _remoteAttackHoldsToRemove = new();

    private sealed class RemoteAttackHoldState(
        EntityUid controller,
        EntityUid remoteEntity,
        RemoteControlAttackHoldType type,
        EntityUid? target,
        EntityCoordinates? coordinates,
        TimeSpan lastUpdated)
    {
        public EntityUid Controller { get; } = controller;
        public EntityUid RemoteEntity { get; } = remoteEntity;
        public RemoteControlAttackHoldType Type { get; } = type;
        public EntityUid? Target { get; set; } = target;
        public EntityCoordinates? Coordinates { get; } = coordinates;
        public TimeSpan LastUpdated { get; } = lastUpdated;
    }

    private void OnRemoteAttackHold(EntityUid uid, RemoteControlConsoleComponent component,
        RemoteControlAttackHoldMessage args)
    {
        var key = (Console: uid, args.AltInteract);
        if (!args.Held)
        {
            RemoveRemoteAttackHold(key, args.Actor);
            return;
        }

        if (!TryGetActionableControlledEntity((uid, component), args.Actor, out var remoteEntity))
        {
            RemoveRemoteAttackHold(key, args.Actor);
            return;
        }

        if ((args.Type == RemoteControlAttackHoldType.MeleeLight && args.AltInteract)
            || (args.Type == RemoteControlAttackHoldType.MeleeHeavy && !args.AltInteract))
        {
            RemoveRemoteAttackHold(key, args.Actor);
            return;
        }

        if (args.Type is not (RemoteControlAttackHoldType.Shoot
            or RemoteControlAttackHoldType.MeleeLight
            or RemoteControlAttackHoldType.MeleeHeavy))
        {
            RemoveRemoteAttackHold(key, args.Actor);
            return;
        }

        var target = args.Target is { } targetNet ? GetEntity(targetNet) : (EntityUid?)null;
        if (target is { } targetEntity && !Exists(targetEntity))
            target = null;

        EntityCoordinates? coordinates = args.HasCoordinates ? GetCoordinates(args.Coordinates) : null;
        _remoteAttackHolds[key] = new RemoteAttackHoldState(
            args.Actor,
            remoteEntity,
            args.Type,
            target,
            coordinates,
            _gameTiming.CurTime);
    }

    private void RemoveRemoteAttackHold((EntityUid Console, bool AltInteract) key, EntityUid controller)
    {
        if (_remoteAttackHolds.TryGetValue(key, out var hold) && hold.Controller == controller)
            _remoteAttackHolds.Remove(key);
    }

    private void UpdateRemoteAttackHolds()
    {
        _remoteAttackHoldsToRemove.Clear();
        UpdateRemoteAttackHolds(true);
        UpdateRemoteAttackHolds(false);

        foreach (var key in _remoteAttackHoldsToRemove)
            _remoteAttackHolds.Remove(key);
    }

    private void UpdateRemoteAttackHolds(bool altInteract)
    {
        var currentTime = _gameTiming.CurTime;
        foreach (var (key, hold) in _remoteAttackHolds)
        {
            if (key.AltInteract != altInteract)
                continue;

            if (currentTime - hold.LastUpdated > RemoteAttackHoldTimeout
                || !TryComp<RemoteControlConsoleComponent>(key.Console, out var console)
                || !TryGetActionableControlledEntity((key.Console, console), hold.Controller, out var remoteEntity)
                || remoteEntity != hold.RemoteEntity)
            {
                _remoteAttackHoldsToRemove.Add(key);
                continue;
            }

            if (!TryComp<CombatModeComponent>(remoteEntity, out var combatMode) || !combatMode.IsInCombatMode)
                continue;

            switch (hold.Type)
            {
                case RemoteControlAttackHoldType.Shoot:
                    {
                        if (hold.Coordinates is not { } shootCoordinates)
                            continue;

                        var gunSystem = EntityManager.System<SharedGunSystem>();
                        if (!gunSystem.TryGetGun(remoteEntity, out var gun)
                            || gun.Comp.UseKey == altInteract
                            || gun.Comp.NextFire > currentTime)
                            continue;

                        if (gun.Comp.BurstActivated)
                            gunSystem.ContinueBurst(remoteEntity, gun);
                        else
                            gunSystem.AttemptShoot(remoteEntity, gun, shootCoordinates, hold.Target);
                        break;
                    }
                case RemoteControlAttackHoldType.MeleeLight:
                    {
                        if (hold.Target is not { } lightTarget || !Exists(lightTarget))
                        {
                            hold.Target = null;
                            continue;
                        }

                        var meleeSystem = EntityManager.System<SharedMeleeWeaponSystem>();
                        if (!meleeSystem.TryGetWeapon(remoteEntity, out var weaponUid, out var weapon)
                            || weapon.NextAttack > currentTime
                            || (TryGetRemotePickupCandidate(remoteEntity, lightTarget, out var hands, out var item)
                                && _hands.CanPickupAnyHand(remoteEntity, lightTarget, handsComp: hands, item: item)))
                            continue;

                        meleeSystem.TryAttemptRemoteLightAttack(remoteEntity, (weaponUid, weapon), lightTarget);
                        break;
                    }
                case RemoteControlAttackHoldType.MeleeHeavy:
                    {
                        if (hold.Coordinates is not { } heavyCoordinates
                            || !_playerManager.TryGetSessionByEntity(hold.Controller, out var session))
                            continue;

                        var meleeSystem = EntityManager.System<SharedMeleeWeaponSystem>();
                        meleeSystem.TryAttemptRemoteHeavyAttackAt(remoteEntity, heavyCoordinates, session);
                        break;
                    }
                default:
                    _remoteAttackHoldsToRemove.Add(key);
                    break;
            }
        }
    }
}
