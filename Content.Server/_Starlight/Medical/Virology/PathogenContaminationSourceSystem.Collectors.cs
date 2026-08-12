using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Disposal.Unit;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// The collectors: every way the station itself becomes a pathogen source. Each pass
/// walks one kind of neglect - rot, puddles, organic trash, dead plants - and reports
/// what it finds through <see cref="AddSource"/>.
/// </summary>
public sealed partial class PathogenContaminationSourceSystem
{
    private void CollectRotSources()
    {
        var corpseContamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationRottingCorpse));
        var foodContamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationRottenFood));

        // Everything perishable in the game is either a mob or edible, so these two branches
        // cover it. Anything that rots and is neither would go uncounted rather than being
        // guessed at.
        //
        // The guards match every other collector: something stowed in a bag, a crate or a
        // disposal unit has been dealt with, and something adrift in space is not the
        // station's problem any more.
        var query = EntityQueryEnumerator<RottingComponent, PerishableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var perishable, out var transform))
        {
            if (!_rotting.IsRotProgressing(uid, perishable) ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null ||
                _containers.IsEntityInContainer(uid))
            {
                continue;
            }

            if (HasComp<MobStateComponent>(uid))
            {
                AddSource(
                    uid,
                    RotSignatures,
                    corpseContamination,
                    PathogenContaminationSourceKind.RottingCorpse);
                continue;
            }

            if (HasComp<EdibleComponent>(uid))
            {
                AddSource(
                    uid,
                    RotSignatures,
                    foodContamination,
                    PathogenContaminationSourceKind.RottingEdible);
            }
        }
    }

    private void CollectRottenFoodSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationRottenFood));

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tags, out var transform))
        {
            if (transform.MapID == MapId.Nullspace ||
                transform.GridUid is null ||
                _containers.IsEntityInContainer(uid) ||
                HasComp<RottingComponent>(uid) ||
                !_tags.HasTag(tags, RottenFoodTag))
            {
                continue;
            }

            AddSource(
                uid,
                RotSignatures,
                contamination,
                PathogenContaminationSourceKind.SpoiledFood);
        }
    }

    private void CollectPuddleSources()
    {
        var biologicalPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationBiologicalPuddlePerUnit));
        var biologicalMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationBiologicalPuddleMaximum));
        var foodPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationFoodPuddlePerUnit));
        var foodMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationFoodPuddleMaximum));
        var waterPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddlePerUnit));
        var waterMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddleMaximum));
        var waterMinimumVolume = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationWaterPuddleMinimumVolume));
        var moldPerUnit = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationMoldPuddlePerUnit));
        var moldMaximum = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationMoldPuddleMaximum));

        var query = EntityQueryEnumerator<PuddleComponent>();
        while (query.MoveNext(out var uid, out var puddle))
        {
            if (!_solutions.ResolveSolution(
                    uid,
                    puddle.SolutionName,
                    ref puddle.Solution,
                    out var solution))
            {
                continue;
            }

            var biologicalVolume = 0f;
            var foodVolume = 0f;
            var moldVolume = 0f;
            var waterVolume = 0f;
            foreach (var quantity in solution.Contents)
            {
                var reagentId = quantity.Reagent.Prototype;
                var volume = quantity.Quantity.Float();

                if (reagentId == NutrimentReagent || reagentId == ProteinReagent)
                    foodVolume += volume;

                if (reagentId == MoldReagent)
                    moldVolume += volume;

                if (reagentId == WaterReagent)
                    waterVolume += volume;

                if (_prototypes.TryIndex<ReagentPrototype>(reagentId, out var reagent) &&
                    reagent.Group == "Biological")
                {
                    biologicalVolume += volume;
                }
            }

            var biologicalContamination = PathogenContaminationMath.PuddleContamination(
                biologicalVolume,
                biologicalPerUnit,
                biologicalMaximum);
            AddSource(
                uid,
                [PathogenType.Bacteria],
                biologicalContamination,
                PathogenContaminationSourceKind.BiologicalPuddle);

            var foodContamination = PathogenContaminationMath.PuddleContamination(
                foodVolume,
                foodPerUnit,
                foodMaximum);
            AddSource(
                uid,
                [PathogenType.Bacteria],
                foodContamination,
                PathogenContaminationSourceKind.FoodPuddle);

            var moldContamination = PathogenContaminationMath.PuddleContamination(
                moldVolume,
                moldPerUnit,
                moldMaximum);
            // Mold feeds both halves of medical neglect: bacterial grime and fungal bloom.
            // Each gets the authored value in full rather than half of it, so a mold puddle
            // is worth twice what a single-type puddle of the same size is.
            AddSource(
                uid,
                [PathogenType.Bacteria, PathogenType.Fungus],
                moldContamination,
                PathogenContaminationSourceKind.MoldPuddle,
                perType: true);

            // Standing water is the one floor source fungus gets, and blood is already
            // bacteria's. The volume gate matters: mopping leaves small water smears
            // behind, and cleaning up blood must not breed fungus as a side effect.
            if (waterVolume >= waterMinimumVolume)
            {
                var waterContamination = PathogenContaminationMath.PuddleContamination(
                    waterVolume,
                    waterPerUnit,
                    waterMaximum);
                AddSource(
                    uid,
                    [PathogenType.Fungus],
                    waterContamination,
                    PathogenContaminationSourceKind.WaterPuddle);
            }
        }
    }

    private void CollectOrganicTrashSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationOrganicTrash));

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tags, out var transform))
        {
            if (transform.MapID == MapId.Nullspace ||
                transform.GridUid is null ||
                _containers.IsEntityInContainer(uid) ||
                !_tags.HasTag(tags, OrganicTrashTag))
            {
                continue;
            }

            AddSource(
                uid,
                [PathogenType.Bacteria],
                contamination,
                PathogenContaminationSourceKind.OrganicTrash);
        }
    }

    private void OnBeingDisposed(Entity<BeingDisposedComponent> entity, ref ComponentStartup args)
    {
        MarkDisposed(entity.Owner);
    }

    /// <summary>
    /// Stamps everything going down a disposal, contents included, so it stays out of the
    /// contamination count once it lands. Marking beats clearing the rubbish tags: rot is
    /// found by component rather than by tag, so tag removal only ever silenced half of it.
    /// </summary>
    private void MarkDisposed(EntityUid uid)
    {
        EnsureComp<PathogenDisposedComponent>(uid);

        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            MarkDisposed(child);
        }
    }

    private void CollectDeadPlantSources()
    {
        var contamination = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationDeadPlant));

        var query = EntityQueryEnumerator<PlantHolderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var plant, out var transform))
        {
            if (!plant.Dead ||
                plant.Seed is null ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null)
            {
                continue;
            }

            AddSource(
                uid,
                [PathogenType.Fungus],
                contamination,
                PathogenContaminationSourceKind.DeadPlant);
        }
    }

    internal float GetViralCarrierContamination()
    {
        var perCarrier = Math.Max(
            0f,
            _config.GetCVar(StarlightCCVars.VirologyContaminationViralCarrier));
        if (perCarrier <= 0f)
            return 0f;

        var carriers = 0;
        var query = EntityQueryEnumerator<PathogenInfectionComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var infections, out var mobState, out var transform))
        {
            if (mobState.CurrentState == MobState.Dead ||
                transform.MapID == MapId.Nullspace ||
                transform.GridUid is null)
            {
                continue;
            }

            if (infections.Infections.Any(infection =>
                    _registry.TryGetStrain(infection.Pathogen, out var strain) &&
                    strain.PathogenType == PathogenType.Virus))
            {
                carriers++;
            }
        }

        return carriers * perCarrier;
    }
}
