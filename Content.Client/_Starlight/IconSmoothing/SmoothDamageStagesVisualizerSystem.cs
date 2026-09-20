using Content.Client.IconSmoothing;
using Content.Shared._Starlight.Structures.DamageStages;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.IconSmoothing;

public sealed partial class SmoothDamageStagesVisualizerSystem : VisualizerSystem<SmoothDamageStagesComponent>
{
    [Dependency] private IconSmoothSystem _iconSmooth = default!;

    protected override void OnAppearanceChange(EntityUid uid, SmoothDamageStagesComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<IconSmoothComponent>(uid, out var smooth))
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, SmoothDamageStageVisuals.Stage, out var stage, args.Component))
            stage = 0;

        var suffix = stage > 0 ? $"_{stage}" : string.Empty;
        if (smooth.StateSuffix == suffix)
            return;

        smooth.StateSuffix = suffix;
        _iconSmooth.DirtyNeighbours(uid, smooth);
    }
}
