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
}
