using System.Numerics;
using Content.Server.Shuttles.Systems;
using Content.Server.UserInterface;
using Content.Shared._Starlight.Weapons.Gunnery;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Weapons.Gunnery;

/// <summary>
/// Server-side logic for the gunnery console.
/// Periodically broadcasts an updated <see cref="GunneryConsoleBoundUserInterfaceState"/>
/// containing the standard radar data, cannon blip positions, and guided-projectile
/// tracking info. Also handles fire and guidance BUI messages from the client.
/// </summary>
public sealed class GunneryConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem   _ui        = default!;
    [Dependency] private readonly ShuttleConsoleSystem  _console   = default!;
    [Dependency] private readonly SharedGunSystem       _gun       = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming           _timing    = default!;

    private const float UpdateInterval = 0.25f;
    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunneryConsoleComponent, ComponentStartup>(OnStartup);

        // Associate newly spawned guided projectiles with the console that fired them.
        SubscribeLocalEvent<GuidedProjectileComponent, ComponentStartup>(OnGuidedProjectileStartup);

        Subs.BuiEvents<GunneryConsoleComponent>(GunneryConsoleUiKey.Key, subs =>
        {
            subs.Event<GunneryConsoleFireMessage>(OnFireMessage);
            subs.Event<GunneryConsoleGuidanceMessage>(OnGuidanceMessage);
        });
    }

    // ── Update loop ────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer = 0f;

        var query = AllEntityQuery<GunneryConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateState(uid, comp);
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnStartup(EntityUid uid, GunneryConsoleComponent comp, ComponentStartup args)
    {
        UpdateState(uid, comp);
    }

    /// <summary>
    /// When a new guided projectile spawns, claim it for the console that most
    /// recently fired (within a 200 ms window).
    /// </summary>
    private void OnGuidedProjectileStartup(EntityUid uid, GuidedProjectileComponent guided, ComponentStartup args)
    {
        var threshold = TimeSpan.FromMilliseconds(200);

        var consoleQuery = AllEntityQuery<GunneryConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var consoleComp, out _))
        {
            if (_timing.CurTime - consoleComp.LastFireTime > threshold)
                continue;

            // Claim this projectile and immediately enable tracking toward the fire target.
            guided.Controller      = consoleUid;
            guided.SteeringTarget  = consoleComp.LastFireTargetPos;
            guided.Active          = true;
            consoleComp.TrackedGuidedProjectile = uid;
            break;
        }
    }

    private void OnFireMessage(EntityUid uid, GunneryConsoleComponent comp, GunneryConsoleFireMessage msg)
    {
        var cannon = GetEntity(msg.Cannon);
        if (!TryComp<GunComponent>(cannon, out var gunComp))
            return;

        var targetCoords = GetCoordinates(msg.Target);

        // Rotate cannon to face the target before firing so it visually aims correctly.
        var cannonMapPos = _transform.GetMapCoordinates(cannon);
        var targetMapPos = _transform.ToMapCoordinates(targetCoords);
        if (cannonMapPos.MapId == targetMapPos.MapId)
        {
            // Robust entity rotation uses 0=south as the sprite default (CCW positive).
            // ToAngle() uses 0=east (standard math). The offset is +π/2 to convert.
            var aimAngle = (targetMapPos.Position - cannonMapPos.Position).ToAngle() + new Angle(Math.PI / 2);
            _transform.SetWorldRotation(cannon, aimAngle);
        }

        // Record fire time and target before shooting so OnGuidedProjectileStartup can claim
        // the spawned entity and activate tracking toward the clicked position.
        comp.LastFireTime       = _timing.CurTime;
        comp.LastFireTargetPos  = targetMapPos.Position;

        // Pass cannon as the "user" so AttemptShoot uses the cannon's world position as the
        // projectile spawn origin instead of the player's position.
        _gun.AttemptShoot(cannon, (cannon, gunComp), targetCoords);
    }

    private void OnGuidanceMessage(EntityUid uid, GunneryConsoleComponent comp, GunneryConsoleGuidanceMessage msg)
    {
        // If no projectile tracked yet, try to find one controlled by this console.
        if (comp.TrackedGuidedProjectile == null || !Exists(comp.TrackedGuidedProjectile.Value))
        {
            comp.TrackedGuidedProjectile = FindControlledProjectile(uid);
            if (comp.TrackedGuidedProjectile == null)
                return;
        }

        if (!TryComp<GuidedProjectileComponent>(comp.TrackedGuidedProjectile.Value, out var guided))
        {
            comp.TrackedGuidedProjectile = null;
            return;
        }

        var targetMapCoords = _transform.ToMapCoordinates(GetCoordinates(msg.Target));
        guided.SteeringTarget = targetMapCoords.Position;
        guided.Active         = true;
        guided.Controller     = uid;
    }

    // ── State building ─────────────────────────────────────────────────────

    private void UpdateState(EntityUid uid, GunneryConsoleComponent comp)
    {
        if (!_ui.HasUi(uid, GunneryConsoleUiKey.Key))
            return;

        var xform             = Transform(uid);
        EntityCoordinates? coordinates = null;
        Angle?             angle       = null;

        if (xform.ParentUid == xform.GridUid)
        {
            coordinates = xform.Coordinates;
            angle       = xform.LocalRotation;
        }

        var docks    = _console.GetAllDocks();
        NavInterfaceState navState;

        if (coordinates != null && angle != null)
            navState = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
        else
            navState = _console.GetNavState(uid, docks);

        navState.MaxRange = comp.MaxRange;

        // Populate standard radar blips (rockets, shells, etc.)
        var consoleMapCoords = _transform.GetMapCoordinates(uid);
        var maxRangeSq       = comp.MaxRange * comp.MaxRange;

        var blipQuery = AllEntityQuery<RadarBlipComponent, TransformComponent>();
        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
        {
            if (blip.RequireInSpace && blipXform.GridUid != null)
                continue;
            if (blipXform.MapID != consoleMapCoords.MapId)
                continue;

            var blipMapCoords = _transform.GetMapCoordinates(blipUid, blipXform);
            if ((blipMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                continue;

            navState.Blips.Add(new RadarBlipData(
                GetNetCoordinates(blipXform.Coordinates),
                blip.Color,
                blip.Scale,
                blip.Shape));
        }

        // Build cannon blip list: all GunneryTrackable guns on the same grid.
        var cannons = new List<CannonBlipData>();
        var gridId  = xform.GridUid;

        if (gridId != null && HasComp<MapGridComponent>(gridId.Value))
        {
            var gunQuery = AllEntityQuery<GunneryTrackableComponent, GunComponent, TransformComponent>();
            while (gunQuery.MoveNext(out var gunUid, out _, out var gunComp, out var gunXform))
            {
                if (gunXform.GridUid != gridId)
                    continue;

                var gunMapCoords = _transform.GetMapCoordinates(gunUid, gunXform);
                if (gunMapCoords.MapId != consoleMapCoords.MapId)
                    continue;

                if ((gunMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                    continue;

                var cooldown = (float)Math.Max(0.0, (gunComp.NextFire - _timing.CurTime).TotalSeconds);
                cannons.Add(new CannonBlipData(
                    GetNetCoordinates(gunXform.Coordinates),
                    GetNetEntity(gunUid),
                    MetaData(gunUid).EntityName,
                    cooldown));
            }
        }

        // Clean up tracked projectile if it has been destroyed.
        if (comp.TrackedGuidedProjectile != null && !Exists(comp.TrackedGuidedProjectile.Value))
            comp.TrackedGuidedProjectile = null;

        var trackedNet = comp.TrackedGuidedProjectile.HasValue
            ? GetNetEntity(comp.TrackedGuidedProjectile.Value)
            : (NetEntity?) null;

        _ui.SetUiState(uid, GunneryConsoleUiKey.Key,
            new GunneryConsoleBoundUserInterfaceState(navState, cannons, trackedNet));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Scans for any <see cref="GuidedProjectileComponent"/> whose controller is this console.</summary>
    private EntityUid? FindControlledProjectile(EntityUid consoleUid)
    {
        var query = AllEntityQuery<GuidedProjectileComponent>();
        while (query.MoveNext(out var uid, out var guided))
        {
            if (guided.Controller == consoleUid)
                return uid;
        }

        return null;
    }
}
