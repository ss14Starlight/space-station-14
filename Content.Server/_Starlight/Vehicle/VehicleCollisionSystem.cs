using Content.Shared._Starlight.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Server.GameTicking;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Vehicle;

public sealed partial class VehicleCollisionSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly EntProtoId CollisionSlowdown = "StatusEffectSlowdownNoMobState";

    [Dependency] private VehicleSystem _vehicles = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private Content.Shared.Administration.Logs.ISharedAdminLogManager _logs = default!;

    [SubscribeLocalEvent]
    private void OnCollision(Entity<VehicleRideComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.ImpactSound is not { } impact || !args.OurFixture.Hard ||
            _vehicles.GetOperatorOrNull(ent.Owner) is not { } driver ||
            !TryComp<VehicleIgnitionComponent>(ent, out var ignition) || !ignition.Started ||
            !TryComp<MovementSpeedModifierComponent>(ent, out var mover))
            return;

        var velocity = args.OurBody.LinearVelocity;
        var speed = velocity.Length();
        var crashSpeed = MathF.Max(8f, MathF.Min(mover.BaseWalkSpeed, mover.BaseSprintSpeed) + 1f);
        if (speed < crashSpeed || args.OtherEntity == ent.Owner || _vehicles.GetOccupiedVehicle(args.OtherEntity) == ent.Owner)
            return;

        if (args.OtherFixture.Hard)
        {
            _audio.PlayPvs(impact, ent);
            if (ent.Comp.EjectOnCrash && _buckle.TryUnbuckle(driver, null, popup: false))
            {
                _stun.TryCrawling(driver, TimeSpan.FromSeconds(3));
                _throwing.TryThrow(driver, velocity, speed, playSound: false);
            }
            else if (_vehicles.TryGetOperatorContainer(ent.Owner, out var cabin))
            {
                foreach (var passenger in cabin.ContainedEntities)
                    _stun.TryAddStunDuration(passenger, TimeSpan.FromSeconds(3));
            }

            return;
        }

        if (_ticker.RunLevel == GameRunLevel.PostRound || !HasComp<DamageableComponent>(args.OtherEntity) ||
            HasComp<PacifiedComponent>(ent) || HasComp<PacifiedComponent>(driver))
            return;

        _audio.PlayPvs(impact, ent);
        var damage = new DamageSpecifier(_prototypes.Index(Blunt), Math.Clamp(10 + 5 * speed / crashSpeed, 10, 20));
        _damageable.TryChangeDamage(args.OtherEntity, damage, origin: driver);
        _movement.TryAddMovementSpeedModDuration(ent.Owner, CollisionSlowdown, TimeSpan.FromSeconds(2), 0.25f);
        _logs.Add(LogType.Damaged, LogImpact.High,
            $"{ToPrettyString(driver)} ran over {ToPrettyString(args.OtherEntity)} with {ToPrettyString(ent)}");
    }
}
