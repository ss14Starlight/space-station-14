using Content.Server._Funkystation.ReagentFires.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;

namespace Content.Server.Fluids.EntitySystems;

/// <summary>
/// Handles solutions on floors. Also handles the spreader logic for where the solution overflows a specified volume.
/// </summary>
public sealed partial class PuddleSystem : SharedPuddleSystem
{
    [Dependency] private ReagentFireSystem _fireSystem = default!; // Funky - Reagent Fires
    // Funky edit - handle reagent fire
    protected override void OnSolutionUpdate(Entity<PuddleComponent> entity, ref SolutionChangedEvent args)
    {
        base.OnSolutionUpdate(entity, ref args);
        _fireSystem.UpdateFire(entity);
    }
    // Funky edit end

}
