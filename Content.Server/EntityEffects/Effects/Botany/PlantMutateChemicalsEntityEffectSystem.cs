using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Botany;
using Content.Shared.FixedPoint;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.EntityEffects.Effects.Botany;

/// <summary>
/// Entity effect that mutates the chemicals of a plant.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class PlantMutateChemicalsEntityEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantMutateChemicals>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantMutateChemicals> args)
    {
        if (!_plantTray.TryGetPlant(entity.AsNullable(), out var plant))
            return;

        var chemicals = EnsureComp<PlantChemicalsComponent>(plant.Value).Chemicals;
        var randomChems = _proto.Index(args.Effect.RandomPickBotanyReagent).Fills;

        // Add a random amount of a random chemical to this set of chemicals.
        var pick = _random.Pick(randomChems);
        var chemicalId = _random.Pick(pick.Reagents);
        var amount = _random.NextFloat(0.1f, (float)pick.Quantity);
        var seedChemQuantity = new PlantChemQuantity();
        if (chemicals.ContainsKey(chemicalId))
        {
            seedChemQuantity.Min = chemicals[chemicalId].Min;
            seedChemQuantity.Max = chemicals[chemicalId].Max + amount;
        }
        else
        {
            //Set the minimum to a fifth of the quantity to give some level of bad luck protection
            seedChemQuantity.Min = FixedPoint2.Clamp(quantity / 5f, FixedPoint2.Epsilon, 1f);
            seedChemQuantity.Max = seedChemQuantity.Min + amount;
            seedChemQuantity.Inherent = false;
        }

        var potencyDivisor = 100f / seedChemQuantity.Max;
        seedChemQuantity.PotencyDivisor = (float)potencyDivisor;
        chemicals[chemicalId] = seedChemQuantity;
    }
}
