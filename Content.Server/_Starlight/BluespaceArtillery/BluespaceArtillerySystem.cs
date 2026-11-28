using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.BluespaceArtillery;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.BluespaceArtillery;

public sealed partial class BluespaceArtillerySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private PowerNetSystem _powerNet = default!;

    private static readonly TimeSpan _uiInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUi;

    public override void Initialize()
    {
        base.Initialize();

        InitializeConsole();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var refreshUi = now >= _nextUi;
        if (refreshUi)
            _nextUi = now + _uiInterval;

        var cannons = EntityQueryEnumerator<BluespaceArtilleryComponent, PowerConsumerComponent>();
        while (cannons.MoveNext(out var uid, out var cannon, out var consumer))
        {
            if (refreshUi)
            {
                UpdateCannonVisuals((uid, cannon), consumer);

                UpdateConsoleOf((uid, cannon));
            }

            if (!cannon.Charging)
            {
                consumer.DrawRate = 0;
                continue;
            }

            consumer.DrawRate = cannon.DrawRate;
            cannon.Stored = Math.Min(cannon.ChargeEnergy, cannon.Stored + (consumer.ReceivedPower * frameTime));

            if (cannon.Stored >= cannon.ChargeEnergy)
            {
                cannon.Charging = false;
                consumer.DrawRate = 0;
                _audio.PlayPvs(cannon.ChargedSound, uid);
                if (TryComp<BluespaceArtilleryConsoleComponent>(cannon.Console, out var console))
                    CheckReadyAlert((cannon.Console.Value, console), cannon);
                UpdateConsoleOf((uid, cannon));
                UpdateCannonVisuals((uid, cannon), consumer);
            }
        }

        var shells = EntityQueryEnumerator<BluespaceArtilleryShellComponent>();
        while (shells.MoveNext(out var uid, out var shell))
        {
            if (now >= shell.ExplodeAt)
                Detonate((uid, shell));
        }

        UpdateTestSites(now);
    }

    /// <summary>
    /// Returns true if artillery charged.
    /// </summary>
    public static bool IsCharged(BluespaceArtilleryComponent cannon) => cannon.Stored >= cannon.ChargeEnergy;

    private void UpdateCannonVisuals(Entity<BluespaceArtilleryComponent> cannon, PowerConsumerComponent? consumer = null)
    {
        if (!Resolve(cannon, ref consumer, false))
            return;

        var supply = consumer.Net?.NetworkNode is { } node
            ? _powerNet.GetNetworkStatistics(node).SupplyTheoretical
            : 0f;

        var state = BluespaceArtilleryState.Idle;
        if (_timing.CurTime < cannon.Comp.CooldownEnd)
            state = BluespaceArtilleryState.Cooldown;
        else if (IsCharged(cannon.Comp))
            state = BluespaceArtilleryState.Ready;
        else if (supply <= 0f)
            state = BluespaceArtilleryState.Unpowered;
        else if (cannon.Comp.Charging)
            state = BluespaceArtilleryState.Charging;

        _appearance.SetData(cannon, BluespaceArtilleryVisuals.State, state);

        if (!TryComp<PointLightComponent>(cannon, out var light))
            return;

        _light.SetEnabled(cannon, state != BluespaceArtilleryState.Unpowered, light);
        _light.SetColor(cannon, state == BluespaceArtilleryState.Cooldown ? cannon.Comp.CooldownLightColor : cannon.Comp.LightColor, light);
        _light.SetEnergy(cannon, state is BluespaceArtilleryState.Ready or BluespaceArtilleryState.Charging ? 2f : 0.8f, light);
    }

    [SubscribeLocalEvent]
    private void OnCannonShutdown(Entity<BluespaceArtilleryComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<BluespaceArtilleryConsoleComponent>(ent.Comp.Console, out var console) && console.Cannon == ent.Owner)
            console.Cannon = null;
    }

    /// <summary>
    /// Tries to fire a charged cannon at the console's target.
    /// </summary>
    public bool TryFire(Entity<BluespaceArtilleryComponent?> cannon, EntityUid target, EntityUid? user, out string? reason)
    {
        if (!Resolve(cannon, ref cannon.Comp, false))
        {
            reason = Loc.GetString("bsa-console-notice-bad");
            return false;
        }

        reason = null;
        var now = _timing.CurTime;

        if (now < cannon.Comp.CooldownEnd)
        {
            reason = Loc.GetString("bsa-console-notice-cooldown");
            return false;
        }

        if (!IsCharged(cannon.Comp))
        {
            reason = Loc.GetString("bsa-console-notice-not-charged");
            return false;
        }

        if (!Exists(target))
        {
            reason = Loc.GetString("bsa-console-notice-no-target");
            return false;
        }

        if (!IsValidTarget(target))
        {
            reason = Loc.GetString("bsa-console-notice-protected");
            return false;
        }

        var xform = Transform(cannon);
        var forward = cannon.Comp.Facing.ToVec();
        var muzzle = xform.Coordinates.Offset(forward * cannon.Comp.MuzzleDistance);
        var worldDirection = _transform.GetWorldRotation(xform).RotateVec(forward);

        var muzzleMap = _transform.ToMapCoordinates(muzzle);
        Spawn(cannon.Comp.MuzzleFlash, muzzleMap);
        var shellUid = Spawn(cannon.Comp.ShellPrototype, muzzleMap);
        _transform.SetWorldRotation(shellUid, worldDirection.ToWorldAngle());
        _physics.SetLinearVelocity(shellUid, worldDirection * cannon.Comp.ShellSpeed);

        var shell = EnsureComp<BluespaceArtilleryShellComponent>(shellUid);
        shell.Cannon = cannon;
        shell.Target = target;
        shell.TargetCoordinates = _transform.GetMapCoordinates(target);
        shell.ExplodeAt = now + cannon.Comp.ShellLifetime;
        shell.ExplosionType = cannon.Comp.ExplosionType;
        shell.TotalIntensity = cannon.Comp.TotalIntensity;
        shell.Slope = cannon.Comp.Slope;
        shell.MaxTileIntensity = cannon.Comp.MaxTileIntensity;
        shell.Hit.Add(cannon);

        cannon.Comp.Stored = 0;
        cannon.Comp.Charging = false;
        cannon.Comp.CooldownEnd = now + cannon.Comp.Cooldown;

        _audio.PlayPvs(cannon.Comp.FireSound, cannon);

        if (user != null)
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"{ToPrettyString(user.Value):player} fired the bluespace artillery {ToPrettyString(cannon):cannon} at {ToPrettyString(target):target} ({shell.TargetCoordinates})");
        }
        else
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"Bluespace artillery {ToPrettyString(cannon):cannon} fired at {ToPrettyString(target):target} ({shell.TargetCoordinates})");
        }

        SendFireAlert(cannon, target, shell.TargetCoordinates, user);
        UpdateConsoleOf((cannon, cannon.Comp));
        UpdateCannonVisuals((cannon, cannon.Comp));
        return true;
    }

    [SubscribeLocalEvent]
    private void OnShellCollide(Entity<BluespaceArtilleryShellComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.Hit.Add(args.OtherEntity))
            return;

        _damageable.TryChangeDamage(args.OtherEntity, ent.Comp.Damage, ignoreResistances: true, origin: ent.Comp.Cannon);
    }

    private void Detonate(Entity<BluespaceArtilleryShellComponent> shell)
    {
        var target = shell.Comp.Target;
        var coordinates = target != null && Exists(target.Value)
            ? _transform.GetMapCoordinates(target.Value)
            : shell.Comp.TargetCoordinates;

        QueueDel(shell);

        if (coordinates.MapId == MapId.Nullspace || !_map.MapExists(coordinates.MapId) || IsProtected(coordinates))
            return;

        _explosion.QueueExplosion(coordinates,
            shell.Comp.ExplosionType,
            shell.Comp.TotalIntensity,
            shell.Comp.Slope,
            shell.Comp.MaxTileIntensity,
            shell.Comp.Cannon);

        if (target != null && TryComp<BluespaceArtilleryTestSiteComponent>(target, out var site))
            OnTestSiteHit((target.Value, site));
    }
}
