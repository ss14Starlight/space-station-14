using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
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

        // When a shuttle console is about to be destroyed, eject any slotted PAI first
        // so the container doesn't delete it.
        SubscribeLocalEvent<ShuttleConsoleComponent, EntityTerminatingEvent>(OnConsoleDying);

        // Event-driven ram detection: fires only when a shuttle grid actually hits something.
        // Subscribes on MapGridComponent rather than ShuttleComponent to avoid conflicting
        // with the existing ShuttleSystem.Impact subscription on that pair.
        SubscribeLocalEvent<MapGridComponent, StartCollideEvent>(OnGridCollide);
    }

    private void OnConsoleDying(EntityUid console, ShuttleConsoleComponent component, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(console, component.PaiSlotId, out var slot))
            return;

        // Collect to avoid modifying while iterating.
        var contained = new List<EntityUid>(slot.ContainedEntities);
        foreach (var ent in contained)
        {
            // Remove pilot status first (if they were actively piloting).
            _shuttleConsole.RemovePilot(ent);

            // Eject from container so it lands at the console's position instead of being deleted.
            _container.Remove(ent, slot, force: true);
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
