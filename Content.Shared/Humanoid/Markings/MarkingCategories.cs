using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings
{
    [Serializable, NetSerializable]
    public enum MarkingCategories : byte
    {
        Special,
        Hair,
        FacialHair,
        Head,
        HeadTop,
        HeadSide,
        Eyes, // Starlight-edit
        Snout,
        SnoutCover,
        Chest,
        UndergarmentTop,
        UndergarmentBottom,
        Arms,
        Hands, // Starlight-edit
        Legs,
        Feet, // Starlight-edit
        Tail,
        TailExtras, // Starlight-edit
        Overlay,
        FaceCover, // Far Horizons
        FaceCoverCover, // Far Horizons
        BodyCover, // Far Horizons
        BodyCoverCover // Far Horizons
    }

    public static class MarkingCategoriesConversion
    {
        public static MarkingCategories FromHumanoidVisualLayers(HumanoidVisualLayers layer)
        {
            return layer switch
            {
                HumanoidVisualLayers.Special => MarkingCategories.Special,
                HumanoidVisualLayers.Hair => MarkingCategories.Hair,
                HumanoidVisualLayers.FacialHair => MarkingCategories.FacialHair,
                HumanoidVisualLayers.Head => MarkingCategories.Head,
                HumanoidVisualLayers.HeadTop => MarkingCategories.HeadTop,
                HumanoidVisualLayers.HeadSide => MarkingCategories.HeadSide,
                HumanoidVisualLayers.Eyes => MarkingCategories.Eyes, // Starlight-edit
                HumanoidVisualLayers.Snout => MarkingCategories.Snout,
                HumanoidVisualLayers.Chest => MarkingCategories.Chest,
                HumanoidVisualLayers.UndergarmentTop => MarkingCategories.UndergarmentTop,
                HumanoidVisualLayers.UndergarmentBottom => MarkingCategories.UndergarmentBottom,
                HumanoidVisualLayers.RArm => MarkingCategories.Arms,
                HumanoidVisualLayers.LArm => MarkingCategories.Arms,
                HumanoidVisualLayers.RHand => MarkingCategories.Hands, // Starlight-edit
                HumanoidVisualLayers.LHand => MarkingCategories.Hands, // Starlight-edit
                HumanoidVisualLayers.LLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.RLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.LFoot => MarkingCategories.Feet, // Starlight-edit
                HumanoidVisualLayers.RFoot => MarkingCategories.Feet, // Starlight-edit
                HumanoidVisualLayers.TailExtras => MarkingCategories.TailExtras, // Starlight-edit
                HumanoidVisualLayers.FaceCover => MarkingCategories.FaceCover, // Far Horizons
                HumanoidVisualLayers.FaceCoverCover => MarkingCategories.FaceCoverCover, // Far Horizons
                HumanoidVisualLayers.BodyCover => MarkingCategories.BodyCover, // Far Horizons
                HumanoidVisualLayers.BodyCoverCover => MarkingCategories.BodyCoverCover, // Far Horizons
                HumanoidVisualLayers.Tail => MarkingCategories.Tail,
                _ => MarkingCategories.Overlay
            };
        }
    }
}
