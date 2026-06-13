using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Destructible;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.PAI;

/// <summary>
/// Ejects a PAI from the shuttle console when the shuttle rams another grid hard enough.
/// The PAI is physically thrown clear. Also prevents the PAI being deleted when the console is destroyed.
/// Uses event-driven collision detection via StartCollideEvent — no per-tick polling.
/// </summary>
public sealed class PAIShuttleRamSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    private const float MinVelocityEpsilon = 0.01f;

    public override void Initialize()
    {
        base.Initialize();

        // When a shuttle console is destroyed (fires before QueueDel), clean up any
        // active pilots so PilotComponent is removed and the player's input is never left stuck.
        SubscribeLocalEvent<ShuttleConsoleComponent, DestructionEventArgs>(OnConsoleDestroyed);

        // Catch-all for any deletion that doesn't go through DestructionEventArgs
        // (e.g. admin tools, map unload). Also ejects any PAI still in the container.
        SubscribeLocalEvent<ShuttleConsoleComponent, EntityTerminatingEvent>(OnConsoleDying);

        // Event-driven ram detection: fires only when a shuttle grid actually hits something.
        // Subscribes on MapGridComponent rather than ShuttleComponent to avoid conflicting
        // with the existing ShuttleSystem.Impact subscription on that pair.
        SubscribeLocalEvent<MapGridComponent, StartCollideEvent>(OnGridCollide);
    }

    /// <summary>
    /// Fires during <see cref="DestructionEventArgs"/>, BEFORE <c>QueueDel</c> is called.
    /// The console entity and all its components are fully alive at this point, so
    /// <c>RemovePilot</c> works correctly and can clean up the pilot's input state.
    /// </summary>
    private void OnConsoleDestroyed(EntityUid console, ShuttleConsoleComponent component, DestructionEventArgs args)
    {
        CleanupPilots(component);
    }

    /// <summary>
    /// Fires when the entity starts terminating (during <c>DeleteEntity</c>).
    /// Handles deletions that don't go through <see cref="DestructionEventArgs"/>.
    /// Also forcibly ejects any PAI still in the container slot so it isn't deleted with the console.
    /// Note: <c>ejectOnBreak: true</c> on the YAML slot handles the common destructible case; this
    /// is a belt-and-suspenders fallback.
    /// </summary>
    private void OnConsoleDying(EntityUid console, ShuttleConsoleComponent component, ref EntityTerminatingEvent args)
    {
        // Clean up any pilots that weren't already handled by OnConsoleDestroyed.
        CleanupPilots(component);

        // Eject any PAI still physically in the container so it isn't voided with the console.
        if (!_container.TryGetContainer(console, component.PaiSlotId, out var slot))
            return;

        var contained = new List<EntityUid>(slot.ContainedEntities);
        foreach (var ent in contained)
        {
            _container.Remove(ent, slot, force: true);
        }
    }

    /// <summary>
    /// Remove <see cref="PilotComponent"/> from every entity currently piloting this console.
    /// Safe to call during both <see cref="DestructionEventArgs"/> and <see cref="EntityTerminatingEvent"/>;
    /// will no-op on entities that have already had the component removed.
    /// </summary>
    private void CleanupPilots(ShuttleConsoleComponent component)
    {
        var pilots = new List<EntityUid>(component.SubscribedPilots);
        foreach (var pilot in pilots)
        {
            // RemovePilot handles the full cleanup (alerts, zoom, popup, RemComp).
            _shuttleConsole.RemovePilot(pilot);

            // Fallback: explicitly remove PilotComponent in case RemovePilot returned early
            // (e.g. the pilot was not in SubscribedPilots for some reason, or the console
            // component was already partially torn down).
            RemComp<PilotComponent>(pilot);
        }
    }

    private void OnGridCollide(EntityUid uid, MapGridComponent _, ref StartCollideEvent args)
    {
        // Only care about shuttles (grids with a ShuttleComponent).
        if (!TryComp<ShuttleComponent>(uid, out var shuttle))
            return;

        // Calculate relative closing speed along the contact normal (mirrors ShuttleSystem.Impact logic).
        var relVel = args.OurBody.LinearVelocity - args.OtherBody.LinearVelocity;
        var speedSq = relVel.LengthSquared();

        Vector2 throwDir;

        // Bias toward head-on impacts; side-scrapes have a near-zero dot product.
        if (args.WorldNormal != Vector2.Zero)
        {
            var normal = args.WorldNormal.Normalized();
            var alongNormal = MathF.Abs(Vector2.Dot(relVel, normal));
            if (alongNormal < shuttle.RamVelocityThreshold)
                return;

            throwDir = args.WorldNormal.Normalized();
        }
        else if (speedSq < shuttle.RamVelocityThreshold * shuttle.RamVelocityThreshold)
            return;
        else
            throwDir = speedSq > MinVelocityEpsilon ? relVel.Normalized() : new Vector2(1f, 0f);

        // Find every PAI currently piloting a console on this shuttle grid.
        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var consoleEnt, out var shuttleConsole, out var transform))
        {
            if (transform.GridUid != uid || !_container.TryGetContainer(consoleEnt, shuttleConsole.PaiSlotId, out var slot) || slot.ContainedEntities.Count == 0)
                continue;

            // Collect to avoid modifying while iterating.
            var ents = new List<EntityUid>(slot.ContainedEntities);
            foreach (var ent in ents)
                EjectPAI(ent, throwDir, slot, shuttleConsole.ItemThrowSpeedOnRam);
        }
    }

    private void EjectPAI(EntityUid uid, Vector2 throwDir, BaseContainer slot, float throwSpeed)
    {
        // Step 1: Remove pilot — this also fires the "shuttle-pilot-end" popup internally.
        _shuttleConsole.RemovePilot(uid);

        // Step 2: Eject the PAI from the console container so it exists in the world.
        _container.Remove(uid, slot, force: true);

        // Step 3: Show the ram popup and throw it.
        _popup.PopupEntity(Loc.GetString("pai-shuttle-rammed"), uid, PopupType.LargeCaution);
        _throwing.TryThrow(uid, throwDir, throwSpeed, playSound: false);
    }
}
