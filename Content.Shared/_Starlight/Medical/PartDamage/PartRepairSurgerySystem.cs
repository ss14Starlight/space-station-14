// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Medical.Surgery.Events;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.PartDamage;

/// <summary>
/// Surgery step effect: restores the operated part's integrity to full.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepRestoreIntegrityEffectComponent : Component;

/// <summary>
/// Surgery condition: only offered on a part whose integrity is not full.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartIntegrityConditionComponent : Component;

/// <summary>
/// Integrity does not come back with ordinary treatment; tissue repair surgery is how a part is made whole again.
/// </summary>
public sealed class PartRepairSurgerySystem : EntitySystem
{
    [Dependency] private BodyPartDamageSystem _partDamage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryStepRestoreIntegrityEffectComponent, SurgeryStepEvent>(OnRestoreIntegrity);
        SubscribeLocalEvent<SurgeryPartIntegrityConditionComponent, SurgeryValidEvent>(OnIntegrityCondition);
    }

    private void OnRestoreIntegrity(Entity<SurgeryStepRestoreIntegrityEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp<BodyPartDamageComponent>(args.Part, out var damage))
            _partDamage.SetIntegrity(args.Body, (args.Part, damage), damage.MaxIntegrity);
    }

    private void OnIntegrityCondition(Entity<SurgeryPartIntegrityConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<BodyPartDamageComponent>(args.Part, out var damage) || damage.Integrity >= damage.MaxIntegrity)
            args.Cancelled = true;
    }
}
