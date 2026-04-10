using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.PAI;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
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

    private const string PaiSlotId = "pai_slot";

    /// Minimum relative closing speed (m/s) along the contact normal to count as a ram.
    private const float RamVelocityThreshold = 5f;

    /// Speed the PAI entity is physically thrown when rammed out.
    private const float ThrowSpeed = 6f;

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

    private void OnConsoleDying(EntityUid console, ShuttleConsoleComponent _, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(console, PaiSlotId, out var slot))
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
        if (!HasComp<ShuttleComponent>(uid))
            return;

        // Calculate relative closing speed along the contact normal (mirrors ShuttleSystem.Impact logic).
        var relVel = args.OurBody.LinearVelocity - args.OtherBody.LinearVelocity;
        var closingSpeed = relVel.Length();
        if (closingSpeed < RamVelocityThreshold)
            return;

        // Bias toward head-on impacts; side-scrapes have a near-zero dot product.
        if (relVel != Vector2.Zero && args.WorldNormal != Vector2.Zero)
            closingSpeed *= MathF.Abs(Vector2.Dot(relVel.Normalized(), args.WorldNormal.Normalized()));

        if (closingSpeed < RamVelocityThreshold)
            return;

        var throwDir = relVel.LengthSquared() > 0.01f ? relVel.Normalized() : new Vector2(1f, 0f);

        // Find every PAI currently piloting a console on this shuttle grid.
        var toEject = new List<EntityUid>();
        var pilotQuery = EntityQueryEnumerator<PAIComponent, PilotComponent>();
        while (pilotQuery.MoveNext(out var paiUid, out var pai, out var pilot))
        {
            if (pilot.Console is not { } consoleEnt)
                continue;
            if (Transform(consoleEnt).GridUid != uid)
                continue;
            toEject.Add(paiUid);
        }

        foreach (var paiUid in toEject)
            EjectPAI(paiUid, throwDir);
    }

    private void EjectPAI(EntityUid uid, Vector2 throwDir)
    {
        // Step 1: Remove pilot — this also fires the "shuttle-pilot-end" popup internally.
        _shuttleConsole.RemovePilot(uid);

        // Step 2: Eject the PAI from the console container so it exists in the world.
        if (_container.TryGetContainingContainer(uid, out var slot) && slot.ID == PaiSlotId)
            _container.Remove(uid, slot, force: true);

        // Step 3: Show the ram popup and throw it.
        _popup.PopupEntity(Loc.GetString("pai-shuttle-rammed"), uid, PopupType.LargeCaution);
        _throwing.TryThrow(uid, throwDir, ThrowSpeed, playSound: false);
    }
}
