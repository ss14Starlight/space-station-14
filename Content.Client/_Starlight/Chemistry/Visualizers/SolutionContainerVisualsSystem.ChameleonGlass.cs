// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Chemistry.Visualizers;

public sealed partial class SolutionContainerVisualsSystem
{
    private Color GetChameleonGlassFillColor(AppearanceChangeEvent args, Color solutionColor)
    {
        if (args.TryGetData<string>(ChameleonGlassVisuals.Override, out var disguise) &&
            !string.IsNullOrEmpty(disguise) &&
            _prototype.TryIndex<ReagentPrototype>(disguise, out var reagent))
            return reagent.SubstanceColor;

        return solutionColor;
    }

    private void ApplyChameleonGlassAppearance(EntityUid uid,
        SolutionContainerVisualsComponent component,
        AppearanceChangeEvent args,
        float fraction,
        int fillLayer,
        int baseLayer,
        int overlayLayer,
        bool hasOverlay,
        ref int maxFillLevels,
        ref string? fillBaseName,
        ref bool changeColor,
        ref SpriteSpecifier? fillSprite)
    {
        if (args.Sprite is not { } sprite ||
            !args.TryGetData<string>(ChameleonGlassVisuals.Override, out var chameleonOverride))
            return;

        if (!string.IsNullOrEmpty(chameleonOverride) &&
            _prototype.TryIndex<ReagentPrototype>(chameleonOverride, out var disguise) &&
            disguise.MetamorphicSprite is { } glassSprite)
        {
            SpriteSystem.LayerSetSprite((uid, sprite), baseLayer, glassSprite);
            maxFillLevels = disguise.MetamorphicMaxFillLevels;
            fillBaseName = disguise.MetamorphicFillBaseName;
            fillSprite = glassSprite;
            changeColor = disguise.MetamorphicChangeColor;
            SpriteSystem.LayerSetVisible((uid, sprite), fillLayer, maxFillLevels > 0);
            if (hasOverlay)
                SpriteSystem.LayerSetVisible((uid, sprite), overlayLayer, false);
        }
        else if (fraction == 0f && component.MetamorphicDefaultSprite != null)
        {
            SpriteSystem.LayerSetSprite((uid, sprite), baseLayer, component.MetamorphicDefaultSprite);
            if (hasOverlay)
                SpriteSystem.LayerSetVisible((uid, sprite), overlayLayer, true);
        }
    }
}
