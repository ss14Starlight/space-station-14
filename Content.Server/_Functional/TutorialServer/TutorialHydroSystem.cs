using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Forces tutorial hydro trays to harvest-ready after planting, and advances HydroHarvest goals.
/// Subscribes on <see cref="TutorialHydroTrayComponent"/> (not PlantHolder) to avoid duplicate
/// directed Comp+Event subscriptions with <see cref="PlantHolderSystem"/>.
/// </summary>
public sealed partial class TutorialHydroSystem : EntitySystem
{
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    private static readonly ProtoId<TagPrototype> HydroTag = "TutorialHydroTray";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialHydroTrayComponent, InteractUsingEvent>(OnInteractUsing, after: [typeof(PlantHolderSystem)]);
        SubscribeLocalEvent<TutorialHydroTrayComponent, InteractHandEvent>(OnInteractHand, after: [typeof(PlantHolderSystem)]);
    }

    private void OnInteractUsing(Entity<TutorialHydroTrayComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tags.HasTag(ent.Owner, HydroTag))
            return;

        if (!TryComp<PlantHolderComponent>(ent, out var plant) || plant.Seed == null || plant.Dead)
            return;

        ForceHarvestReady((ent.Owner, plant));
        ent.Comp.AwaitingHarvestResult = true;
        Dirty(ent);
    }

    private void OnInteractHand(Entity<TutorialHydroTrayComponent> ent, ref InteractHandEvent args)
    {
        if (!_tags.HasTag(ent.Owner, HydroTag))
            return;

        if (!ent.Comp.AwaitingHarvestResult)
            return;

        if (!TryComp<PlantHolderComponent>(ent, out var plant))
            return;

        // Successful harvest clears the Harvest flag; if it is still set, the click did nothing.
        if (plant.Harvest)
            return;

        ent.Comp.AwaitingHarvestResult = false;
        ent.Comp.Harvested = true;
        Dirty(ent);

        if (!TryComp<TutorialParticipantComponent>(args.User, out var part))
            return;

        if (!_tutorial.TryGetCurrentSubGoal(args.User, part, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.HydroHarvest)
            return;

        if (sub.Entity != null)
        {
            var matched = false;
            foreach (var held in _hands.EnumerateHeld(args.User))
            {
                var meta = MetaData(held);
                if (meta.EntityPrototype?.ID == sub.Entity.Value.Id)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return;
        }

        _tutorial.AdvanceSubGoal(args.User);
    }

    private void ForceHarvestReady(Entity<PlantHolderComponent> ent)
    {
        var plant = ent.Comp;
        if (plant.Seed == null)
            return;

        plant.Dead = false;
        plant.Age = (int) Math.Max(plant.Seed.Maturation, plant.Seed.Production) + 1;
        plant.LastProduce = plant.Age - (int) plant.Seed.Production - 1;
        plant.Harvest = true;
        plant.Health = plant.Seed.Endurance;
        plant.NutritionLevel = 50;
        plant.WaterLevel = 50;
        plant.UpdateSpriteAfterUpdate = true;
        _plantHolder.CheckLevelSanity(ent, plant);
        _plantHolder.UpdateSprite(ent, plant);
    }
}
