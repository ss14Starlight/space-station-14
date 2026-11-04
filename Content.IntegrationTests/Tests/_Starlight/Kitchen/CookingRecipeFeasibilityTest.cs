#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Kitchen;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Kitchen;

/// <summary>
/// Checks cooking recipe loading, ingredient capacity, and recipe matching against real appliances.
/// Does not simulate chemical reactions, cooking, or ingredient availability during a round.
/// </summary>
[TestFixture]
public sealed class CookingRecipeFeasibilityTest : GameTest
{
    private EntityCoordinates _coordinates;

    [Test]
    public async Task EveryCookingRecipeCanBeLoadedAndMatched()
    {
        var map = await Pair.CreateTestMap();
        _coordinates = map.GridCoords;
        await Server.WaitAssertion(() =>
        {
            var prototypes = SProtoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract && !Pair.IsTestPrototype(p))
                .OrderBy(p => p.ID)
                .ToArray();
            var appliances = prototypes
                .Where(p => p.TryComp<CookingDeviceComponent>(out _, SEntMan.ComponentFactory))
                .ToArray();
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var carriers = new List<(EntityUid Uid, FixedPoint2 Volume)>();

            try
            {
                foreach (var prototype in prototypes)
                {
                    if (!prototype.TryComp<ItemComponent>(out _, SEntMan.ComponentFactory) ||
                        !prototype.TryComp<DrainableSolutionComponent>(out _, SEntMan.ComponentFactory) ||
                        !prototype.TryComp<RefillableSolutionComponent>(out _, SEntMan.ComponentFactory))
                        continue;

                    var uid = SSpawnAtPosition(prototype.ID, _coordinates);
                    if (solutions.TryGetDrainableSolution(uid, out _, out var solution) && solution.MaxVolume > 0)
                        carriers.Add((uid, solution.MaxVolume));
                    else
                        SDeleteNow(uid);
                }

                var recipes = SProtoMan.EnumeratePrototypes<FoodRecipePrototype>()
                    .Where(p => !Pair.IsTestPrototype(p)).OrderBy(p => p.ID).ToArray();
                Assert.That(recipes, Is.Not.Empty);
                Assert.That(appliances, Is.Not.Empty);

                using (Assert.EnterMultipleScope())
                {
                    foreach (var recipe in recipes)
                    {
                        var failures = new List<string>();
                        var feasible = false;
                        foreach (var prototype in appliances)
                        {
                            prototype.TryComp<CookingDeviceComponent>(out var device, SEntMan.ComponentFactory);
                            if (device!.DeviceType != recipe.DeviceType)
                                continue;

                            var failure = CheckAppliance(prototype, recipe, carriers, containers, solutions);
                            if (failure == null)
                            {
                                feasible = true;
                                break;
                            }

                            failures.Add($"{prototype.ID}: {failure}");
                        }

                        Assert.That(feasible, Is.True,
                            $"Recipe {recipe.ID} -> {recipe.Result} cannot be loaded and matched. " +
                            (failures.Count == 0
                                ? $"No {recipe.DeviceType} appliance exists."
                                : string.Join("; ", failures)));
                    }
                }
            }
            finally
            {
                foreach (var carrier in carriers)
                    SDeleteNow(carrier.Uid);
                SDeleteNow(map.MapUid);
            }
        });
    }

    private string? CheckAppliance(
        EntityPrototype prototype,
        FoodRecipePrototype recipe,
        List<(EntityUid Uid, FixedPoint2 Volume)> carriers,
        SharedContainerSystem containers,
        SharedSolutionContainerSystem solutions)
    {
        var appliance = SEntity<CookingDeviceComponent>(SSpawnAtPosition(prototype.ID, _coordinates));
        var spawned = new List<EntityUid>();
        try
        {
            if (recipe.SecretRecipe)
            {
                var ev = new GetSecretRecipesEvent();
                SEntMan.EventBus.RaiseLocalEvent(appliance.Owner, ref ev);
                if (!ev.Recipes.Contains(recipe))
                    return "does not provide this secret recipe";
            }

            var solids = new Dictionary<string, int>();
            var stacks = SEntMan.System<SharedStackSystem>();
            var availableVolume = FixedPoint2.Zero;
            var containedReagents = new Dictionary<string, FixedPoint2>();
            foreach (var (ingredient, quantity) in recipe.IngredientsSolids)
            {
                if (quantity <= 0 || quantity != quantity.Int())
                    return $"{ingredient} has invalid solid quantity {quantity}";

                var remaining = quantity.Int();
                while (remaining > 0)
                {
                    var uid = SSpawnAtPosition(ingredient, _coordinates);
                    spawned.Add(uid);
                    var count = 1;
                    if (STryComp<StackComponent>(uid, out var stack))
                    {
                        var stackId = SProtoMan.Index(stack.StackTypeId).Spawn;
                        if (stackId.Id != ingredient.Id)
                            return $"{ingredient} is counted as {stackId} by cooking";

                        count = Math.Min(remaining, stacks.GetMaxCount(stack));
                        if (count <= 0)
                            return $"{ingredient} has no usable stack capacity";
                        stacks.SetCount((uid, stack), count);
                    }

                    if (!containers.Insert(uid, appliance.Comp.Storage))
                    {
                        var size = "no Item";
                        if (STryComp<ItemComponent>(uid, out var item))
                        {
                            var itemSize = item.Size;
                            size = itemSize.ToString();
                        }
                        return $"cannot insert {ingredient} ({size}); maximum size {appliance.Comp.MaxItemSize}, " +
                            $"capacity {appliance.Comp.Capacity}, already loaded {appliance.Comp.Storage.Count}";
                    }

                    if (solutions.TryGetDrainableSolution(uid, out _, out var drainable))
                    {
                        foreach (var (reagent, amount) in drainable.Contents)
                        {
                            containedReagents.TryGetValue(reagent.Prototype, out var existing);
                            containedReagents[reagent.Prototype] = existing + amount;
                        }

                        if (solutions.TryGetRefillableSolution(uid, out _, out var refillable) &&
                            ReferenceEquals(refillable, drainable))
                            availableVolume += drainable.AvailableVolume;
                    }

                    remaining -= count;
                }

                solids.Add(ingredient, quantity.Int());
            }

            var reagentVolume = FixedPoint2.Zero;
            foreach (var (reagent, quantity) in recipe.IngredientsReagents)
            {
                containedReagents.TryGetValue(reagent.Id, out var existing);
                if (quantity > existing)
                    reagentVolume += quantity - existing;
            }
            if (reagentVolume > availableVolume)
            {
                var carrier = carriers.Where(c => containers.CanInsert(c.Uid, appliance.Comp.Storage))
                    .OrderByDescending(c => c.Volume).FirstOrDefault();
                if (carrier.Volume <= 0)
                    return $"no room for a refillable, drainable reagent container ({reagentVolume - availableVolume}u needed)";

                var needed = (int)Math.Ceiling((double)(reagentVolume - availableVolume).Float() / carrier.Volume.Float());
                if (needed > appliance.Comp.Capacity - appliance.Comp.Storage.Count)
                    return $"reagent containers require {needed} slots in addition to {appliance.Comp.Storage.Count} solid slots";

                var carrierId = SComp<MetaDataComponent>(carrier.Uid).EntityPrototype!.ID;
                for (var i = 0; i < needed; i++)
                {
                    var uid = SSpawnAtPosition(carrierId, _coordinates);
                    spawned.Add(uid);
                    if (!containers.Insert(uid, appliance.Comp.Storage))
                        return $"cannot insert reagent container {carrierId}";
                }
            }

            if (recipe.CookTime == 0 || recipe.IngredientsReagents.Any(r => r.Value.Int() <= 0))
                return "invalid cooking time or reagent quantity";

            var reagents = recipe.IngredientsReagents.ToDictionary(r => r.Key.Id, r => r.Value);
            for (uint time = 5; time <= appliance.Comp.MaxCookTime; time += 5)
            {
                appliance.Comp.CurrentCookTimerTime = time;
                if (CookingDeviceSystem.CanSatisfyRecipe(appliance.Comp, recipe, solids, reagents).Item2 > 0)
                    return null;
            }

            return $"no selectable cooking time matches recipe time {recipe.CookTime}s (maximum {appliance.Comp.MaxCookTime}s)";
        }
        finally
        {
            foreach (var uid in spawned)
                SDeleteNow(uid);
            SDeleteNow(appliance.Owner);
        }
    }
}
