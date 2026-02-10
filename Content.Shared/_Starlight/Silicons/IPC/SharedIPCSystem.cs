// IPC System - Main (Shared)
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Silicons.IPC;

public abstract partial class SharedIPCSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    
    /// <summary>
    /// Flash duration multiplier for IPCs (2.5x = 250% longer flash)
    /// IPCs have sensitive optical sensors that take longer to reset after exposure
    /// </summary>
    private const float IpcFlashMultiplier = 2.5f;

    public override void Initialize()
    {
        base.Initialize();
        SetupBattery();
        SetupSparkEffects();
        
        // Robots don't sleep
        SubscribeLocalEvent<IPCComponent, TryingToSleepEvent>(OnTryingToSleep);
        
        // Optical sensors are vulnerable to bright light
        SubscribeLocalEvent<IPCComponent, AfterFlashedEvent>(OnFlashed);
        
        // Show IPC-specific status when examined
        SubscribeLocalEvent<IPCComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateBattery(frameTime);
    }
    
    /// <summary>
    /// IPCs cannot sleep - they're robots, not biological entities.
    /// Blocks beds, sleep verbs, and sleep-inducing effects.
    /// </summary>
    private void OnTryingToSleep(EntityUid uid, IPCComponent component, ref TryingToSleepEvent args)
    {
        args.Cancelled = true;
    }
    
    /// <summary>
    /// Extends flash duration for IPCs after they've been successfully flashed.
    /// Rationale: IPCs use optical sensors (cameras/photodiodes) that lack biological
    /// protective mechanisms like pupil constriction or reflexive blinking.
    /// </summary>
    private void OnFlashed(EntityUid uid, IPCComponent component, ref AfterFlashedEvent args)
    {
        // AfterFlashedEvent fires AFTER the flash was successfully applied
        // We extend the duration by modifying the status effect
        if (!_statusEffects.HasStatusEffect(uid, "Flashed"))
            return;
            
        // Get current time window (start time, end time) and multiply remaining duration
        if (_statusEffects.TryGetTime(uid, "Flashed", out var times))
        {
            var (startTime, endTime) = times.Value;
            var remainingTime = endTime - _timing.CurTime;
            var newDuration = remainingTime * IpcFlashMultiplier;
            _statusEffects.TrySetTime(uid, "Flashed", newDuration);
        }
    }
    
    /// <summary>
    /// Shows IPC-specific information when examined.
    /// Identifies the entity as robotic and displays relevant status.
    /// </summary>
    private void OnExamined(EntityUid uid, IPCComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
            
        args.PushMarkup(Loc.GetString("ipc-examine-text"));
    }

    /// <summary>
    /// Gets the IPC's battery and tries to use some charge from it, returning true if successful.
    /// Serverside only. Similar to ninja's TryUseCharge implementation.
    /// </summary>
    public virtual bool TryUseCharge(EntityUid user, float charge)
    {
        return false;
    }
}



