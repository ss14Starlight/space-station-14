using Content.Shared._Funkystation.Fluids;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!; // Funky - Clothing stains
    [Dependency] private StandingStateSystem _standing = default!; // Moff - Clothing stains
    [Dependency] private SharedGravitySystem _gravity = default!; // Moff - Clothing Stains

    // Moff start - we basically rewrote this function compared to what funky has
    // Using startcollide rather than onstep, since the onstep is messed with by slippable... its bleak
    [SubscribeLocalEvent]
    private void OnStepInPuddle(Entity<PuddleComponent> ent, ref StartCollideEvent args)
    {
        // If it dont stain it dont stain
        if (!ent.Comp.CausesStains)
            return;

        // The thing stepping in the puddle. Because I keep forgetting which is which
        var stepper = args.OtherEntity;

        if (!_solutionContainerSystem.ResolveSolution(ent.Owner, ent.Comp.SolutionName, ref ent.Comp.Solution, out var solution))
            return;

        if (solution.Volume <= FixedPoint2.Zero)
            return;

        // Check if its in air... because... if you're not on the ground you don't get spilled on
        if (TryComp<PhysicsComponent>(stepper, out var physicsComp)
            && (physicsComp.BodyStatus == BodyStatus.InAir || _gravity.IsWeightless(stepper)))
            return;

        // Choose le target...
        // if standing and have shoes, just get it on their shoes
        EntityUid target;
        if (_standing.IsDown(stepper)) // on the ground, spill it on them in general
            target = stepper;
        else if (_inventory.TryGetSlotEntity(stepper, "shoes", out var shoes) && shoes is { } shoeUid)
            target = shoeUid;
        else
            return;

        var spilledEvent = new SpilledOnEvent(ent.Owner, solution);
        RaiseLocalEvent(target, spilledEvent);
    }
    // Moff end
}
