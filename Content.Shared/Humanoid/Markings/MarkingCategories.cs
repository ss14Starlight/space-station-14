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
        Eyes, // Starlight
        Snout,
        SnoutCover,
        Chest,
        UndergarmentTop,
        UndergarmentBottom,
        Arms,
        Hands, // Starlight
        Legs,
        Feet, // Starlight
        Tail,
        TailExtras, // Starlight
        Overlay
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
                HumanoidVisualLayers.Eyes => MarkingCategories.Eyes, // Starlight
                HumanoidVisualLayers.Snout => MarkingCategories.Snout,
                HumanoidVisualLayers.Chest => MarkingCategories.Chest,
                HumanoidVisualLayers.UndergarmentTop => MarkingCategories.UndergarmentTop,
                HumanoidVisualLayers.UndergarmentBottom => MarkingCategories.UndergarmentBottom,
                HumanoidVisualLayers.RArm => MarkingCategories.Arms,
                HumanoidVisualLayers.LArm => MarkingCategories.Arms,
                HumanoidVisualLayers.RHand => MarkingCategories.Hands, // Starlight
                HumanoidVisualLayers.LHand => MarkingCategories.Hands, // Starlight
                HumanoidVisualLayers.LLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.RLeg => MarkingCategories.Legs,
                HumanoidVisualLayers.LFoot => MarkingCategories.Feet, // Starlight
                HumanoidVisualLayers.RFoot => MarkingCategories.Feet, // Starlight
                HumanoidVisualLayers.TailExtras => MarkingCategories.TailExtras, // Starlight
                HumanoidVisualLayers.Tail => MarkingCategories.Tail,
                _ => MarkingCategories.Overlay
            };
        }
    }
}
