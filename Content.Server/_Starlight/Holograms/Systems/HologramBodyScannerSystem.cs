using Content.Server.Mind;
using Content.Server.Medical.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
///     Handles hologram body scanner functionality.
///     Scans a person to create brain and body chips for holographic projection.
/// </summary>
public sealed class HologramBodyScannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<HologramBodyScannerComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, HologramBodyScannerComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Check scan cooldown
        var currentTime = _timing.CurTime;
        if (currentTime < component.LastScanTime + component.ScanDelay)
        {
            _popup.PopupEntity("The scanner is still processing the last scan!", uid, args.User);
            return;
        }

        // Check if scanner has a body in it
        if (!TryComp<MedicalScannerComponent>(uid, out var scanner))
            return;

        // Check if body container has contents
        if (scanner.BodyContainer.ContainedEntity == null)
        {
            _popup.PopupEntity("The scanner is empty!", uid, args.User);
            return;
        }

        var scannedEntity = scanner.BodyContainer.ContainedEntity.Value;

        // Must have a mind to scan
        if (!TryComp<MindContainerComponent>(scannedEntity, out var mindContainer) || mindContainer.Mind == null)
        {
            _popup.PopupEntity("The scanner cannot detect a consciousness to transfer!", uid, args.User);
            return;
        }

        // Perform the scan - create both chips
        var scannerXform = Transform(uid);
        var spawnPos = scannerXform.Coordinates;

        // Create brain chip with mind
        var brainChip = Spawn("HologramBrainChip", spawnPos);
        if (TryComp<HologramBrainChipComponent>(brainChip, out var brainComp))
        {
            // Force mind transfer to chip
            var mind = mindContainer.Mind.Value;
            brainComp.HoloMind = mind;
            
            // Transfer the mind to the chip
            _mind.TransferTo(mind, brainChip);
            
            _popup.PopupEntity($"Mind transferred to brain chip!", uid, args.User);
        }

        // Create body chip with appearance data
        var bodyChip = Spawn("HologramBodyChip", spawnPos);
        if (TryComp<HologramBodyChipComponent>(bodyChip, out var bodyComp))
        {
            // Store name and prototype for body
            var meta = MetaData(scannedEntity);
            bodyComp.HologramName = meta.EntityName;
            
            // For now, store the prototype ID (later could store full appearance)
            bodyComp.HologramPrototype = MetaData(scannedEntity).EntityPrototype?.ID;
            
            _popup.PopupEntity($"Body data saved to body chip!", uid, args.User);
        }

        component.LastScanTime = currentTime;
        args.Handled = true;
    }
}
