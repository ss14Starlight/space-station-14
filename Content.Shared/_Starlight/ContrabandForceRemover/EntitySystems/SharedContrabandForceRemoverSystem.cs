using System.Linq;
using Content.Shared._Starlight.ContrabandForceRemover.Components;
using Content.Shared._Starlight.ScanGate;
using Content.Shared._Starlight.ScanGate.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.DeviceLinking;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.ContrabandForceRemover.EntitySystems;

/// <summary>
/// System that handles contraband detection and removal for the contraband force remover gate.
/// Combines scan gate contraband detection with job-based access control.
/// </summary>
public sealed class SharedContrabandForceRemoverSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ContrabandForceRemoverComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ContrabandForceRemoverComponent, EndCollideEvent>(OnEndCollide);

        base.Initialize();
    }

    private void OnCollide(EntityUid uid, ContrabandForceRemoverComponent component, ref StartCollideEvent args)
    {
        var entity = args.OtherEntity;
        
        // Don't scan if already scanned recently (prevents spam)
        if (component.PassingThrough.Contains(entity))
            return;

        if (component.NextScanTime > _gameTiming.CurTime
            || !_powerReceiverSystem.IsPowered(uid))
            return;
        
        // Skip if not a mob or doesn't have contraband capability
        if (!HasComp<HandsComponent>(entity) && !HasComp<InventoryComponent>(entity))
            return;

        // Mark as scanned IMMEDIATELY to prevent multiple scans/sounds
        component.PassingThrough.Add(entity);
        component.NextScanTime = _gameTiming.CurTime + component.ScanDelay;
        Dirty(uid, component);

        // Scan for contraband
        var contraband = FindContraband(entity);
        
        if (contraband.Count > 0)
        {
            // Check if person has valid access for their contraband
            var blockedItems = new List<EntityUid>();
            
            foreach (var item in contraband)
            {
                if (!TryComp<ContrabandComponent>(item, out var contrabandComp))
                    continue;

                // Check if this contraband is allowed for this entity
                if (!IsContrabandAllowedForEntity(uid, entity, item, contrabandComp, component))
                {
                    blockedItems.Add(item);
                }
            }

            if (blockedItems.Count > 0)
            {
                // Remove contraband but allow passage (scan gate behavior)
                HandleBlockedContraband(uid, entity, blockedItems, component);
                return;
            }
        }

        // No contraband or all allowed
        NoContrabandDetected(uid, component);
    }

    private void OnEndCollide(EntityUid uid, ContrabandForceRemoverComponent component, ref EndCollideEvent args)
    {
        component.PassingThrough.Remove(args.OtherEntity);
        Dirty(uid, component);
    }

    /// <summary>
    /// Finds all contraband items on an entity (in hands and inventory).
    /// </summary>
    private List<EntityUid> FindContraband(EntityUid entity)
    {
        var contraband = new List<EntityUid>();

        // Check hands
        if (TryComp<HandsComponent>(entity, out var hands))
        {
            // The key in hands.Hands dictionary IS the container name
            foreach (var handId in hands.Hands.Keys)
            {
                // Get the container for this hand
                if (_container.TryGetContainer(entity, handId, out var container) &&
                    container.ContainedEntities.Count > 0)
                {
                    var heldEntity = container.ContainedEntities[0];
                    
                    if (HasComp<ContrabandComponent>(heldEntity))
                    {
                        contraband.Add(heldEntity);
                    }
                    
                    // Check storage in held item
                    if (TryComp<StorageComponent>(heldEntity, out var storage))
                    {
                        CheckStorageForContraband(storage, contraband);
                    }
                }
            }
        }

        // Check inventory
        if (TryComp<InventoryComponent>(entity, out var inventory))
        {
            if (_inventory.TryGetSlots(entity, out var slots))
            {
                foreach (var slot in slots)
                {
                    if (_inventory.TryGetSlotEntity(entity, slot.Name, out var slotEntity, inventoryComponent: inventory))
                    {
                        if (HasComp<ContrabandComponent>(slotEntity.Value))
                        {
                            contraband.Add(slotEntity.Value);
                        }

                        // Check storage in inventory item
                        if (TryComp<StorageComponent>(slotEntity.Value, out var storage))
                        {
                            CheckStorageForContraband(storage, contraband);
                        }
                    }
                }
            }
        }

        return contraband;
    }

    /// <summary>
    /// Recursively checks storage containers for contraband.
    /// </summary>
    private void CheckStorageForContraband(StorageComponent storage, List<EntityUid> contraband)
    {
        foreach (var (item, _) in storage.StoredItems)
        {
            if (HasComp<ContrabandComponent>(item))
            {
                contraband.Add(item);
            }

            // Recursively check nested storage
            if (TryComp<StorageComponent>(item, out var nestedStorage))
            {
                CheckStorageForContraband(nestedStorage, contraband);
            }
        }
    }

    /// <summary>
    /// Checks if an entity is allowed to have specific contraband based on severity level and their job/department.
    /// </summary>
    private bool IsContrabandAllowedForEntity(EntityUid gate, EntityUid entity, EntityUid contrabandItem, ContrabandComponent contraband, ContrabandForceRemoverComponent component)
    {
        var severity = contraband.Severity;

        // Check if this is always blocked contraband (Major, Syndicate, Magical, Soviet, AdvancedCyberlimbs)
        if (component.AlwaysBlockedSeverities.Contains(severity))
            return false;

        // Get the ID card to check job/department
        if (!_idCard.TryFindIdCard(entity, out var idCard))
            return false;

        var departments = idCard.Comp.JobDepartments;
        var jobTitle = idCard.Comp.LocalizedJobTitle;

        // HighlyIllegal: Only Central Command can pass
        if (component.CentralCommandOnlySeverities.Contains(severity))
        {
            return departments.Contains(component.CentralCommandDepartment);
        }

        // GrandTheft: Only Command members can pass
        if (component.CommandOnlySeverities.Contains(severity))
        {
            return departments.Intersect(component.CommandDepartments).Any();
        }

        // Minor: Security and Command can pass
        if (component.CommandSecuritySeverities.Contains(severity))
        {
            return departments.Contains(component.SecurityDepartment) || 
                   departments.Intersect(component.CommandDepartments).Any();
        }

        // Job-specific severities (like TSF): Check if job/department matches allowed lists
        if (component.JobSpecificSeverities.Contains(severity))
        {
            // Check if job is explicitly allowed
            if (jobTitle != null)
            {
                var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
                if (jobs.Contains(jobTitle))
                    return true;
            }
            // For job-specific severities, must have exact job match
            return false;
        }

        // Departmentally restricted: Check if department/job matches allowed lists
        if (component.DepartmentRestrictedSeverities.Contains(severity))
        {
            // Check if job is explicitly allowed
            if (jobTitle != null)
            {
                var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
                if (jobs.Contains(jobTitle))
                    return true;
            }

            // Check if department matches
            if (departments.Intersect(contraband.AllowedDepartments).Any())
                return true;

            return false;
        }

        // Default: Allow if job or department matches
        if (jobTitle != null)
        {
            var jobs = contraband.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
            if (jobs.Contains(jobTitle))
                return true;
        }

        if (departments.Intersect(contraband.AllowedDepartments).Any())
            return true;

        return false;
    }

    /// <summary>
    /// Handles contraband that was detected and blocked.
    /// </summary>
    private void HandleBlockedContraband(EntityUid uid, EntityUid entity, List<EntityUid> blockedItems, ContrabandForceRemoverComponent component)
    {
        // Play fail sound and animation
        _audio.PlayPvs(component.ScanFailSound, uid);
        SetState(uid, component, component.ScanFailState);
        _deviceLink.InvokePort(uid, component.FailSignal);

        // Show popup to entity
        _popup.PopupEntity(
            Loc.GetString("contraband-force-remover-blocked",
                ("count", blockedItems.Count)),
            entity,
            entity,
            PopupType.LargeCaution);

        // Delete all confiscated contraband
        foreach (var item in blockedItems)
        {
            QueueDel(item);
        }
    }

    /// <summary>
    /// Called when no contraband is detected or all contraband is allowed.
    /// </summary>
    private void NoContrabandDetected(EntityUid uid, ContrabandForceRemoverComponent component)
    {
        _audio.PlayPvs(component.ScanSound, uid);
        SetState(uid, component, component.ScanSuccessState);
        _deviceLink.InvokePort(uid, component.SuccessSignal);
    }

    /// <summary>
    /// Sets the visual state of the gate and resets it to idle after 1 second.
    /// </summary>
    private void SetState(EntityUid uid, ContrabandForceRemoverComponent component, string state)
    {
        _appearanceSystem.SetData(uid, ScanGateVisuals.State, state);
        Timer.Spawn(TimeSpan.FromSeconds(1), () => 
        {
            if (Exists(uid))
                _appearanceSystem.SetData(uid, ScanGateVisuals.State, component.IdleState);
        });
    }
}
