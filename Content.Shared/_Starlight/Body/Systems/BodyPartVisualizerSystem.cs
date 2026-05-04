// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Humanoid.Events;
using Content.Shared.Mobs;
using Content.Shared.Preferences;

namespace Content.Shared._Starlight.Body.Systems;

public sealed class BodyPartVisualizerSystem : EntitySystem
{
    [Dependency] private readonly StarlightEntitySystem _sl = default!;
    [Dependency] private readonly SLBodySystem _body = default!;
    [Dependency] private readonly SharedBodyVisualizerSystem _bodyVisualizer = default!;

    public override void Initialize()
    {
        base.Initialize();
        // what about the part component?
        _body.SubscribeBodyEvent<ApplyAppearanceEvent/*,BodyVisualizerComponent*/>(OnApplyBodyPartAppearance);
    }

    private void OnApplyBodyPartAppearance(
        Entity<SLBodyComponent> body,
        Entity<SLBodyPartComponent> part,
        ref ApplyAppearanceEvent args)
    {
        if (!_sl.TryEntity<SLBodyComponent, BodyVisualizerComponent>(body, out var bodyVis))
            return;

        if (!_sl.TryEntity<SLBodyPartComponent, BodyPartVisualizerComponent>(part, out var partVis))
            return;

        if (args.Profile is { } profile)
        {
            foreach (var layer in partVis.Comp2.BodyVisualLayers)
            {
                _bodyVisualizer.SetPartLayerColor(bodyVis, partVis, layer.Key, GetColor(layer.Value, profile));
            }
        }
    }

    // For now, the main thing is to get everything running on adapters. We can rework the contract later.
    private static Color GetColor(BodySpriteSpecifier value, HumanoidCharacterProfile profile)
        => value.ColorSource switch
        {
            BodyPartColorSource.None => value.SpriteColor,
            BodyPartColorSource.SkinColor => profile.Appearance.SkinColor,
            BodyPartColorSource.EyeColor => profile.Appearance.EyeColor,
            BodyPartColorSource.HairColor => profile.Appearance.HairColor,
            BodyPartColorSource.FacialHairColor  => profile.Appearance.FacialHairColor,
            _ => value.SpriteColor,
        };
}
