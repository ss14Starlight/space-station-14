using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// Shared helpers for food/reagent allergy matching and taste-popup append text.
/// </summary>
public sealed class SharedAllergySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    /// <summary>
    /// If the eater is allergic to this bite, returns a localized allergy name for taste append.
    /// Also sets <see cref="AllergyComponent.PendingTasteAllergyName"/> when found.
    /// </summary>
    public bool TryGetIngestAllergyName(
        EntityUid eater,
        EntityUid food,
        Solution? swallowed,
        out string allergyName)
    {
        allergyName = string.Empty;
        if (!TryComp<AllergyComponent>(eater, out var allergy))
            return false;

        var foodId = MetaData(food).EntityPrototype?.ID;
        if (foodId == null)
            return false;

        foreach (var allergyId in allergy.Allergies)
        {
            if (!_prototypes.TryIndex(allergyId, out AllergyPrototype? proto))
                continue;

            if (!FoodMatchesAllergy(foodId, swallowed, proto))
                continue;

            allergyName = FormatTasteAllergyName(proto);
            allergy.PendingTasteAllergyName = allergyName;
            Dirty(eater, allergy);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Short substance name for taste popups ("Wheat") rather than full record name ("Wheat Allergy").
    /// </summary>
    public string FormatTasteAllergyName(AllergyPrototype proto)
    {
        var name = Loc.GetString(proto.Name);
        const string allergySuffix = " Allergy";
        const string contraindicationSuffix = " Contraindication";

        if (name.EndsWith(allergySuffix, StringComparison.Ordinal))
            return name[..^allergySuffix.Length];

        if (name.EndsWith(contraindicationSuffix, StringComparison.Ordinal))
            return name[..^contraindicationSuffix.Length];

        return name;
    }

    /// <summary>
    /// Consumes and returns any pending taste-append allergy name for this eater.
    /// </summary>
    public bool TryTakePendingTasteAllergy(EntityUid eater, out string allergyName)
    {
        allergyName = string.Empty;
        if (!TryComp<AllergyComponent>(eater, out var allergy) ||
            string.IsNullOrEmpty(allergy.PendingTasteAllergyName))
            return false;

        allergyName = allergy.PendingTasteAllergyName;
        allergy.PendingTasteAllergyName = null;
        Dirty(eater, allergy);
        return true;
    }

    public bool FoodMatchesAllergy(EntProtoId foodId, Solution? swallowed, AllergyPrototype allergy)
    {
        if (swallowed != null && allergy.TriggerReagents.Any(reagent =>
                swallowed.GetTotalPrototypeQuantity(reagent) > 0))
            return true;

        if (allergy.TriggerFoods.Contains(foodId))
            return true;

        return allergy.TriggerFoodRoots.Any(root => IsPrototypeOrDescendant(foodId, root));
    }

    /// <summary>
    /// Estimates how much allergen was in this exposure. Used to scale duration and intensity.
    /// </summary>
    public float GetExposureUnits(Solution? swallowed, AllergyPrototype allergy)
    {
        if (swallowed == null || swallowed.Volume <= FixedPoint2.Zero)
            return 1f;

        var allergen = FixedPoint2.Zero;
        foreach (var reagent in allergy.TriggerReagents)
            allergen += swallowed.GetTotalPrototypeQuantity(reagent);

        if (allergen > FixedPoint2.Zero)
            return Math.Clamp(allergen.Float(), 0.5f, 12f);

        // Prototype-family match with no tagged reagents: scale by swallowed volume.
        // ~5u is a typical bite transfer.
        return Math.Clamp(swallowed.Volume.Float() / 5f, 0.5f, 6f);
    }

    public PopupType GetCautionPopupType(AllergySeverity severity)
    {
        return severity >= AllergySeverity.Severe ? PopupType.LargeCaution : PopupType.SmallCaution;
    }

    private bool IsPrototypeOrDescendant(EntProtoId foodId, EntProtoId rootId)
    {
        if (foodId == rootId)
            return true;

        var pending = new Stack<EntProtoId>();
        var visited = new HashSet<EntProtoId>();
        pending.Push(foodId);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) ||
                !_prototypes.TryIndex(current, out EntityPrototype? prototype) ||
                prototype.Parents == null)
            {
                continue;
            }

            foreach (var parent in prototype.Parents)
            {
                if (parent == rootId)
                    return true;
                pending.Push(parent);
            }
        }

        return false;
    }
}
