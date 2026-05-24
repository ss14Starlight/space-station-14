// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Humanoid.Events;
using Content.Shared.Mobs;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Systems;

public sealed class BodyPartVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
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

    private Color GetColor(BodySpriteSpecifier value, HumanoidCharacterProfile profile)
    {
        if (value.ColorSource is not { } source)
            return value.SpriteColor;

        // TODO: Resolve from per-character body editor parameters once they're persisted on HumanoidCharacterProfile.
        return _proto.TryIndex(source, out var proto) ? proto.DefaultColor : value.SpriteColor;
    }
}
