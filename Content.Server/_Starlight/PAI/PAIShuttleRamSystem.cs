using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.PAI;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Server.PAI;

/// <summary>
/// Ejects a PAI from the shuttle console when the shuttle rams another grid hard enough.
/// The PAI is physically thrown clear. Also prevents the PAI being deleted when the console is destroyed.
/// </summary>
public sealed class PAIShuttleRamSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    private const string PaiSlotId = "pai_slot";

    /// Minimum shuttle delta-V (m/s) in a single tick that counts as a ram.
    private const float RamDeltaVThreshold = 0.8f;

    /// Speed the PAI entity is physically thrown when rammed out.
    private const float ThrowSpeed = 6f;

    public override void Initialize()
    {
        base.Initialize();

        // When a shuttle console is about to be destroyed, eject any slotted PAI first
        // so the container doesn't delete it.
        SubscribeLocalEvent<ShuttleConsoleComponent, EntityTerminatingEvent>(OnConsoleDying);
    }

    private void OnConsoleDying(EntityUid console, ShuttleConsoleComponent _, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(console, PaiSlotId, out var slot))
            return;

        // Collect to avoid modifying while iterating.
        var contained = new List<EntityUid>(slot.ContainedEntities);
        foreach (var ent in contained)
        {
            if (!HasComp<PAIComponent>(ent))
                continue;

            // Remove pilot status first (if they were actively piloting).
            _shuttleConsole.RemovePilot(ent);

            // Eject from container so it lands at the console's position instead of being deleted.
            _container.Remove(ent, slot, force: true);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toEject = new List<(EntityUid Uid, Vector2 Dir)>();
        var toClean = new List<EntityUid>();

        // Walk all PAIs that are actively piloting a console.
        var pilotQuery = EntityQueryEnumerator<PAIComponent, PilotComponent>();
        while (pilotQuery.MoveNext(out var uid, out _, out var pilot))
        {
            if (pilot.Console is not { } consoleEnt)
                continue;

            var tracker = EnsureComp<PAIShuttlePilotingComponent>(uid);

            if (tracker.ShuttleGrid == default)
            {
                var xform = Transform(consoleEnt);
                if (xform.GridUid is { } grid)
                {
                    tracker.ShuttleGrid = grid;
                    tracker.LastShuttleVelocity = TryComp<PhysicsComponent>(grid, out var initBody)
                        ? initBody.LinearVelocity
                        : Vector2.Zero;
                }
                continue; // skip collision check on the seeding tick
            }

            if (TryComp<PhysicsComponent>(tracker.ShuttleGrid, out var body))
            {
                var deltaV = (body.LinearVelocity - tracker.LastShuttleVelocity).Length();
                tracker.LastShuttleVelocity = body.LinearVelocity;

                if (deltaV >= RamDeltaVThreshold)
                {
                    var throwDir = tracker.LastShuttleVelocity.LengthSquared() > 0.01f
                        ? tracker.LastShuttleVelocity.Normalized()
                        : new Vector2(1f, 0f);
                    toEject.Add((uid, throwDir));
                }
            }
        }

        // Clean up tracking components from PAIs no longer piloting.
        var cleanQuery = EntityQueryEnumerator<PAIShuttlePilotingComponent>();
        while (cleanQuery.MoveNext(out var uid, out _))
        {
            if (!HasComp<PilotComponent>(uid))
                toClean.Add(uid);
        }

        foreach (var (uid, dir) in toEject)
            EjectPAI(uid, dir);

        foreach (var uid in toClean)
            RemCompDeferred<PAIShuttlePilotingComponent>(uid);
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
