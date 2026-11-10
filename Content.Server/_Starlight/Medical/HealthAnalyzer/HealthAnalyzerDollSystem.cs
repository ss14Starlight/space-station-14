// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.PartDamage;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Medical.HealthAnalyzer;

public sealed partial class HealthAnalyzerDollSystem : EntitySystem
{
    [Dependency] private BodyPartDamageSystem _partDamage = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SLBodySystem _body = default!;

    [SubscribeLocalEvent]
    private void OnCollect(Entity<SLBodyComponent> body, ref CollectHealthAnalyzerExtensionsEvent args)
    {
        var levels = new Dictionary<string, byte>();

        if (_prototypes.TryIndex(body.Comp.Prefab, out var prefab))
            CollectExpected(prefab.Root, null, levels);

        foreach (var part in _body.GetBodyParts(body))
        {
            if (GetDollPart(part.Comp.Kind, _body.GetPartSide(part)) is not { } name)
                continue;

            var level = TryComp<BodyPartDamageComponent>(part, out var damage) ? GetLevel(damage) : (byte) 0;
            levels[name] = levels.TryGetValue(name, out var old) && old != HealthAnalyzerDollPart.Missing
                ? Math.Max(old, level)
                : level;
        }

        if (levels.TryGetValue("chest", out var chest))
            levels["groin"] = chest;

        foreach (var (name, level) in levels)
            args.Doll.Add(new HealthAnalyzerDollPart { Part = name, Level = level });
    }

    private void CollectExpected(BodyPartDef def, string? socketId, Dictionary<string, byte> levels)
    {
        if (_prototypes.TryIndex(def.BodyPart, out var proto)
            && proto.TryComp<SLBodyPartComponent>(out var part, _factory))
        {
            var side = part.Side;
            if (side == BodySide.None && socketId != null)
            {
                side = socketId.StartsWith("Left", StringComparison.Ordinal) ? BodySide.Left
                    : socketId.StartsWith("Right", StringComparison.Ordinal) ? BodySide.Right
                    : BodySide.None;
            }

            if (GetDollPart(part.Kind, side) is { } name)
                levels[name] = HealthAnalyzerDollPart.Missing;
        }

        if (def.AttachedParts == null)
            return;
        foreach (var (childSocket, child) in def.AttachedParts)
            CollectExpected(child, childSocket, levels);
    }

    private byte GetLevel(BodyPartDamageComponent damage)
    {
        var health = _partDamage.GetHealthFraction(damage);
        var integrity = damage.MaxIntegrity > 0 ? (damage.Integrity / damage.MaxIntegrity).Float() : 1f;
        return MathF.Min(health, integrity) switch
        {
            > 0.9f => 0,
            > 0.75f => 1,
            > 0.6f => 2,
            > 0.4f => 3,
            > 0.2f => 4,
            _ => 5,
        };
    }

    private static string? GetDollPart(BodyPartType kind, BodySide side) => (kind, side) switch
    {
        (BodyPartType.Head, _) => "head",
        (BodyPartType.Torso, _) => "chest",
        (BodyPartType.Arm, BodySide.Left) => "l_arm",
        (BodyPartType.Arm, BodySide.Right) => "r_arm",
        (BodyPartType.Hand, BodySide.Left) => "l_hand",
        (BodyPartType.Hand, BodySide.Right) => "r_hand",
        (BodyPartType.Leg, BodySide.Left) => "l_leg",
        (BodyPartType.Leg, BodySide.Right) => "r_leg",
        (BodyPartType.Foot, BodySide.Left) => "l_foot",
        (BodyPartType.Foot, BodySide.Right) => "r_foot",
        _ => null,
    };
}
