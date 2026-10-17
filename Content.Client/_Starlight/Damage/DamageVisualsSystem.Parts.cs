// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Client.Damage;

public sealed partial class DamageVisualsSystem
{
    [Dependency] private BodyPartDamageSystem _partDamage = default!;
    [Dependency] private SLBodySystem _slBody = default!;

    private readonly Dictionary<EntityUid, Dictionary<(Enum Layer, string Group), FixedPoint2>> _partThresholds = [];

    [SubscribeLocalEvent]
    private void OnDamageVisualsShutdown(Entity<DamageVisualsComponent> ent, ref ComponentShutdown args)
        => _partThresholds.Remove(ent);

    [SubscribeLocalEvent]
    private void OnPartDamageState(Entity<BodyPartDamageComponent> part, ref AfterAutoHandleStateEvent args)
    {
        if (CompOrNull<SLBodyPartComponent>(part)?.Body is not { } body
            || !TryComp<DamageVisualsComponent>(body, out var damageVis)
            || !damageVis.Valid
            || damageVis.Disabled
            || damageVis.DamageOverlayGroups == null
            || !TryComp<DamageableComponent>(body, out var damageable)
            || !TryComp<SpriteComponent>(body, out var sprite))
            return;

        TryUpdatePartDamageVisuals(damageVis.DamageOverlayGroups.Keys, (body, damageable, sprite, damageVis));
    }

    private bool TryUpdatePartDamageVisuals(
        IEnumerable<ProtoId<DamageGroupPrototype>> groups,
        Entity<DamageableComponent, SpriteComponent, DamageVisualsComponent> entity)
    {
        var damageVis = entity.Comp3;
        if (damageVis.TargetLayers == null || !TryComp<SLBodyComponent>(entity, out var body))
            return false;

        var parts = new List<(Entity<SLBodyPartComponent> Part, BodyPartDamageComponent Damage)>();
        foreach (var part in _slBody.GetBodyParts((entity.Owner, body)))
        {
            if (TryComp<BodyPartDamageComponent>(part, out var damage))
                parts.Add((part, damage));
        }

        if (!_partThresholds.TryGetValue(entity, out var thresholds))
            _partThresholds[entity] = thresholds = [];
        foreach (var group in groups)
        {
            if (damageVis.DamageOverlayGroups?.ContainsKey(group) != true
                || !_prototypeManager.TryIndex<DamageGroupPrototype>(group, out var groupProto))
                continue;

            foreach (var layer in damageVis.TargetLayerMapKeys)
            {
                var total = FixedPoint2.Zero;
                foreach (var (part, damage) in parts)
                {
                    if (!ShowsOnLayer(part, layer))
                        continue;
                    foreach (var type in groupProto.DamageTypes)
                        total += _partDamage.GetPartDamage(damage, type);
                }

                var threshold = GetThreshold(total, damageVis);
                if (thresholds.TryGetValue((layer, group.Id), out var last) && last == threshold)
                    continue;

                thresholds[(layer, group.Id)] = threshold;
                UpdateTargetLayer((entity, entity.Comp2, damageVis), layer, group.Id, threshold);
            }
        }

        return true;
    }

    private bool ShowsOnLayer(Entity<SLBodyPartComponent> part, Enum layer)
    {
        if (layer is not HumanoidVisualLayers humanoidLayer)
            return false;

        var side = _slBody.GetPartSide(part);
        return (part.Comp.Kind, side, humanoidLayer) switch
        {
            (BodyPartType.Torso, _, HumanoidVisualLayers.Chest) => true,
            (BodyPartType.Head, _, HumanoidVisualLayers.Head) => true,
            (BodyPartType.Arm or BodyPartType.Hand, BodySide.Left, HumanoidVisualLayers.LArm) => true,
            (BodyPartType.Arm or BodyPartType.Hand, BodySide.Right, HumanoidVisualLayers.RArm) => true,
            (BodyPartType.Leg or BodyPartType.Foot, BodySide.Left, HumanoidVisualLayers.LLeg) => true,
            (BodyPartType.Leg or BodyPartType.Foot, BodySide.Right, HumanoidVisualLayers.RLeg) => true,
            _ => false,
        };
    }

    private static FixedPoint2 GetThreshold(FixedPoint2 damage, DamageVisualsComponent damageVis)
    {
        damage /= damageVis.Divisor;
        var index = damageVis.Thresholds.BinarySearch(damage);
        if (index >= 0)
            return damageVis.Thresholds[index];

        index = ~index;
        return damageVis.Thresholds[Math.Max(0, index - 1)];
    }
}
