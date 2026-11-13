using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Reptilian.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Reptilian.Systems;

public sealed partial class FireBreathFuelSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    /// <summary>
    /// Burns the fuel for the strongest tier the mob has enough of, stomach first, then blood.
    /// </summary>
    public FireBreathFuelTier? TryBurnFuel(Entity<FireBreathComponent> ent)
    {
        if (ent.Comp.FuelTiers.Count == 0)
            return null;

        var sources = new List<Entity<SolutionComponent>>();
        foreach (var stomach in _body.GetBodyOrganEntityComps<StomachComponent>((ent.Owner, null)))
        {
            if (_solution.ResolveSolution(stomach.Owner, StomachSystem.DefaultSolutionName, ref stomach.Comp1.Solution, out _))
                sources.Add(stomach.Comp1.Solution.Value);
        }

        if (_solution.TryGetSolution(ent.Owner, BloodstreamComponent.DefaultBloodSolutionName, out var blood))
            sources.Add(blood.Value);

        foreach (var tier in ent.Comp.FuelTiers)
        {
            var total = FixedPoint2.Zero;
            foreach (var source in sources)
                total += source.Comp.Solution.GetTotalPrototypeQuantity(tier.Reagent);

            if (total < tier.Amount)
                continue;

            var left = tier.Amount;
            foreach (var source in sources)
            {
                if (left <= 0)
                    break;
                left -= _solution.RemoveReagent(source, tier.Reagent, left);
            }

            return tier;
        }

        return null;
    }
}
