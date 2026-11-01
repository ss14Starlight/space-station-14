// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Medical.PartDamage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BodyPartDamageSystem))]
public sealed partial class BodyPartDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxHealth = 60;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxIntegrity = 100;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Integrity = 100;

    [DataField]
    public float HitWeight = 1f;

    [DataField]
    public SlotFlags ArmorSlots = SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING;

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> Penetration = new()
    {
        { "Blunt", 0.1f },
        { "Slash", 0.2f },
        { "Piercing", 0.3f },
        { "Heat", 0.15f },
        { "Cold", 0.1f },
        { "Shock", 0.05f },
        { "Caustic", 0.15f },
    };

    [DataField]
    public float DefaultPenetration = 0.1f;

    /// <summary>
    /// Structural damage of one type a single hit has to deal to destroy the part. Anything weaker, like fire,
    /// pressure or a beating, leaves it mangled at the last point of integrity.
    /// </summary>
    [DataField]
    public FixedPoint2 MinDestroyingHit = 15;

    /// <summary>
    /// Damage types that can destroy the part at all; the rest only mangle it.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> DestroyedBy = new() { "Blunt", "Slash", "Piercing", "Heat" };

    /// <summary>
    /// Heat damage on the part at which it burns away, however it got there. Defaults to health plus integrity.
    /// A torso only burns away on a dead body.
    /// </summary>
    [DataField]
    public FixedPoint2? BurnThreshold;
}
