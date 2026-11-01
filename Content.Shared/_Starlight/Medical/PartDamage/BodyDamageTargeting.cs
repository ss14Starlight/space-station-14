// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.PartDamage;

public enum BodyDamageTargetingMode : byte
{
    /// <summary>
    /// Damage goes to one part rolled by hit weights; healing is shared out by how damaged each part is.
    /// </summary>
    Default,

    /// <summary>
    /// Damage is spread over every part by hit weights (explosions).
    /// </summary>
    Spread,

    /// <summary>
    /// Damage goes to a leg or a foot (falls, landing hard).
    /// </summary>
    Legs,

    /// <summary>
    /// Healing goes to the most damaged part first (topicals, menders).
    /// </summary>
    MostDamaged,

    /// <summary>
    /// Healing goes to a random damaged part (reagents).
    /// </summary>
    Random,

    /// <summary>
    /// Damage and healing go to <see cref="BodyDamageTargeting.Part"/>.
    /// </summary>
    Part,
}

/// <summary>
/// Where local damage or healing dealt to a body goes. Set for a call with <see cref="BodyPartDamageSystem.Target"/>.
/// </summary>
public readonly record struct BodyDamageTargeting(BodyDamageTargetingMode Mode, EntityUid? Part = null)
{
    public static readonly BodyDamageTargeting Default = new(BodyDamageTargetingMode.Default);
    public static readonly BodyDamageTargeting Spread = new(BodyDamageTargetingMode.Spread);
    public static readonly BodyDamageTargeting Legs = new(BodyDamageTargetingMode.Legs);
    public static readonly BodyDamageTargeting MostDamaged = new(BodyDamageTargetingMode.MostDamaged);
    public static readonly BodyDamageTargeting Random = new(BodyDamageTargetingMode.Random);

    public static BodyDamageTargeting AtPart(EntityUid part) => new(BodyDamageTargetingMode.Part, part);
}

/// <summary>
/// Raised on a damageable entity before resistances and armor apply, so locational damage can pick the part that
/// gets hit; only the armor in <see cref="ArmorSlots"/> then protects against it.
/// </summary>
[ByRefEvent]
public record struct DamageTargetPartEvent(DamageSpecifier Damage, EntityUid? Origin)
{
    public SlotFlags? ArmorSlots;
}

/// <summary>
/// Raised on the body to adjust how likely each part is to be hit (pose, attack direction, source...).
/// </summary>
[ByRefEvent]
public record struct BodyPartHitWeightsEvent(List<(EntityUid Part, float Weight)> Weights, EntityUid? Origin);

/// <summary>
/// Raised on a body part and on its body when the part's integrity changes. <see cref="Overkill"/> is how far below
/// zero the hit went, for telling a severed limb from a blown off one; <see cref="Cause"/> is the damage type that
/// did it, for telling a severed limb from a burnt one.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartIntegrityChangedEvent(
    EntityUid Body,
    EntityUid Part,
    FixedPoint2 OldIntegrity,
    FixedPoint2 NewIntegrity,
    FixedPoint2 Overkill,
    ProtoId<DamageTypePrototype>? Cause);
