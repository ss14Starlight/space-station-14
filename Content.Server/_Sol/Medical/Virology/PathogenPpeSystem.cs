using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// PPE contamination transfer and unsafe doffing exposure.
/// </summary>
public sealed class PathogenPpeSystem : EntitySystem
{
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PathogenResistanceComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<PathogenResistanceComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<SurfaceContaminationComponent, ContactInteractionEvent>(OnContaminatedContact);
    }

    private void OnEquipped(Entity<PathogenResistanceComponent> gear, ref GotEquippedEvent args)
    {
        // Exterior contamination can transfer from gloves onto newly worn PPE.
        if (!_inventory.TryGetSlotEntity(args.EquipTarget, "gloves", out var gloves))
            return;

        TransferContamination(gloves.Value, gear, fraction: 0.25f);
    }

    private void OnUnequipped(Entity<PathogenResistanceComponent> gear, ref GotUnequippedEvent args)
    {
        if (!TryComp<SurfaceContaminationComponent>(gear, out var surface) || surface.Contaminants.Count == 0)
            return;

        // Safe doffing: wearing gloves while removing contaminated PPE avoids self-exposure.
        var safe = _inventory.TryGetSlotEntity(args.EquipTarget, "gloves", out _);
        if (safe)
        {
            _popup.PopupEntity(Loc.GetString("sol-ppe-safe-doff"), args.EquipTarget, args.EquipTarget);
            return;
        }

        foreach (var entry in surface.Contaminants)
        {
            _pathogen.TryExpose(args.EquipTarget, entry.PathogenId, entry.Load * 0.35f, PathogenTransmission.Contact, gear);
            _pathogen.AddOrIncreaseContamination(args.EquipTarget, entry.PathogenId, entry.Load * 0.2f);
        }

        _popup.PopupEntity(Loc.GetString("sol-ppe-unsafe-doff"), args.EquipTarget, args.EquipTarget, PopupType.MediumCaution);
    }

    private void OnContaminatedContact(Entity<SurfaceContaminationComponent> surface, ref ContactInteractionEvent args)
    {
        if (!_pathogen.IsVirologyEnabledAt(args.Other))
            return;

        foreach (var slot in new[] { "outerClothing", "gloves", "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(args.Other, slot, out var gear))
                continue;

            if (!HasComp<PathogenResistanceComponent>(gear.Value))
                continue;

            TransferContamination(surface, gear.Value, fraction: 0.15f);
            break;
        }
    }

    public void TransferContamination(EntityUid from, EntityUid to, float fraction)
    {
        if (!TryComp<SurfaceContaminationComponent>(from, out var source) || source.Contaminants.Count == 0)
            return;

        foreach (var entry in source.Contaminants)
        {
            var amount = entry.Load * fraction;
            if (amount <= 0.01f)
                continue;

            _pathogen.AddOrIncreaseContamination(to, entry.PathogenId, amount);
        }
    }
}
