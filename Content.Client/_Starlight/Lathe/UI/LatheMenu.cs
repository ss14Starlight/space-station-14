// ReSharper disable CheckNamespace
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Lathe.UI;

public sealed partial class LatheMenu
{
    public Dictionary<ProtoId<MaterialPrototype>, int> Materials = new();

    private bool CanProduceFromState(LatheRecipePrototype prototype, int quantity, LatheComponent? lathe)
    {
        if (quantity <= 0 || lathe == null)
            return false;

        var multiplier = lathe.MaterialUseMultiplier;
        foreach (var (material, needed) in prototype.Materials)
        {
            var adjusted = SharedLatheSystem.AdjustMaterial(needed, prototype.ApplyMaterialDiscount, multiplier);
            if (Materials.GetValueOrDefault(material) < adjusted * quantity)
                return false;
        }
        return true;
    }
}
