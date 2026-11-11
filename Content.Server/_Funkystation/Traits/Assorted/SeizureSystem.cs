// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.Movement.Systems;
using Content.Shared.IdentityManagement;
using Content.Server.Speech.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.Traits.Assorted;

/// <summary>
/// System that handles seizure effects including prodrome warning phase
/// and seizure with stunning, visual effects, and speech impairment.
/// </summary>
public sealed class SeizureSystem : EntitySystem
{
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeizureComponent, ComponentStartup>(OnSeizureStart);
        SubscribeLocalEvent<SeizureComponent, ComponentShutdown>(OnSeizureEnd);
        SubscribeLocalEvent<SeizureComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<SeizureOverlayComponent, RefreshMovementSpeedModifiersEvent>(OnOverlayRefreshMovementSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SeizureComponent>();
        while (query.MoveNext(out var uid, out var seizure))
        {
            // dead people cant have seizures
            if (_mobState.IsDead(uid))
                continue;

            seizure.RemainingTime -= frameTime;

            UpdateMovementSpeed(uid, seizure, frameTime);

            // Update SeizureOverlayComponent visual effect
            if (TryComp<SeizureOverlayComponent>(uid, out var overlayComp))
            {
                overlayComp.PulseAccumulator += frameTime;

                if (overlayComp.IsFading)
                {
                    var fadeSpeed = 1.0f / overlayComp.FadeOutDuration;
                    var fadeAmount = fadeSpeed * frameTime;

                    overlayComp.BlurryMagnitude = MathHelper.Lerp(overlayComp.BlurryMagnitude, 0f, fadeAmount);
                    overlayComp.PulseAmplitude = MathHelper.Lerp(overlayComp.PulseAmplitude, 0f, fadeAmount);
                    overlayComp.CurrentBlur = MathHelper.Lerp(overlayComp.CurrentBlur, 0f, fadeAmount);

                    if (overlayComp.BlurryMagnitude <= 0.01f && overlayComp.PulseAmplitude <= 0.01f && overlayComp.CurrentBlur <= 0.01f)
                    {
                        RemComp<SeizureOverlayComponent>(uid);
                        continue;
                    }
                }
                else
                {
                    var targetBlur = overlayComp.BlurryMagnitude;
                    var rampSpeed = targetBlur > overlayComp.CurrentBlur
                        ? overlayComp.RampUpSpeed
                        : overlayComp.RampDownSpeed;

                    overlayComp.CurrentBlur = MathHelper.Lerp(overlayComp.CurrentBlur, targetBlur, frameTime * rampSpeed);
                }

                Dirty(uid, overlayComp);
            }

            if (seizure.RemainingTime <= 0f)
            {
                // Transition to next phase or end
                if (seizure.CurrentState == SeizureState.Prodrome)
                {
                    // Transition from prodrome to seizure
                    StartSeizurePhase(uid, seizure);
                }
                else if (seizure.CurrentState == SeizureState.Seizure)
                {
                    // Transition from seizure to recovery
                    StartRecoveryPhase(uid, seizure);
                }
                else if (seizure.CurrentState == SeizureState.Recovery)
                {
                    // Recovery is over, start overlay fade but keep SeizureComponent until fade completes
                    if (TryComp<SeizureOverlayComponent>(uid, out var recoveryOverlayComp))
                    {
                        recoveryOverlayComp.IsFading = true;
                        recoveryOverlayComp.FadeOutDuration = 1f;

                        // Transfer current movement speed to overlay for gradual recovery
                        recoveryOverlayComp.MovementSpeedMultiplier = seizure.MovementSpeedMultiplier;
                        recoveryOverlayComp.HandleMovementRecovery = true;

                        // Mark seizure as in fade phase
                        seizure.CurrentState = SeizureState.Fading;
                        seizure.RemainingTime = recoveryOverlayComp.FadeOutDuration;

                        Dirty(uid, recoveryOverlayComp);
                    }
                    else
                    {
                        // No overlay to fade, remove component
                        RemComp<SeizureComponent>(uid);
                    }
                }
                else
                {
                    // Fading phase is complete
                    RemComp<SeizureComponent>(uid);
                }
                continue;
            }

            // Apply appropriate effects based on current state
            if (seizure.CurrentState == SeizureState.Seizure)
            {
                // jittering during seizure only
                _jittering.DoJitter(uid, TimeSpan.FromSeconds(1f), true,
                    seizure.JitterAmplitude, seizure.JitterFrequency, true);
            }
        }

        // Handle SeizureOverlayComponent
        var overlayQuery = EntityQueryEnumerator<SeizureOverlayComponent>();
        while (overlayQuery.MoveNext(out var overlayUid, out var overlay))
        {
            // dead people still cant have seizures
            if (_mobState.IsDead(overlayUid))
                continue;

            overlay.PulseAccumulator += frameTime;

            if (overlay.HandleMovementRecovery)
            {
                var speedChangeRate = 0.3f;
                overlay.MovementSpeedMultiplier = MathHelper.Lerp(overlay.MovementSpeedMultiplier, 1.0f, frameTime * speedChangeRate);

                _movementSpeed.RefreshMovementSpeedModifiers(overlayUid);

                if (MathF.Abs(overlay.MovementSpeedMultiplier - 1.0f) < 0.01f)
                {
                    overlay.MovementSpeedMultiplier = 1.0f;
                    overlay.HandleMovementRecovery = false;
                    _movementSpeed.RefreshMovementSpeedModifiers(overlayUid);
                }
            }

            // Handle overlay fade logic
            if (overlay.IsFading)
            {
                var fadeSpeed = 1.0f / overlay.FadeOutDuration;
                var fadeAmount = fadeSpeed * frameTime;

                // During fade
                overlay.BlurryMagnitude = MathHelper.Lerp(overlay.BlurryMagnitude, 0f, fadeAmount);
                overlay.PulseAmplitude = MathHelper.Lerp(overlay.PulseAmplitude, 0f, fadeAmount);
                overlay.CurrentBlur = MathHelper.Lerp(overlay.CurrentBlur, 0f, fadeAmount);

                // Remove when fade is complete
                if (overlay.BlurryMagnitude <= 0.01f && overlay.PulseAmplitude <= 0.01f && overlay.CurrentBlur <= 0.01f)
                {
                    RemComp<SeizureOverlayComponent>(overlayUid);
                    continue;
                }
            }
            else
            {
                // interpolate CurrentBlur to target
                var targetBlur = overlay.BlurryMagnitude;
                var rampSpeed = targetBlur > overlay.CurrentBlur
                    ? overlay.RampUpSpeed
                    : overlay.RampDownSpeed;

                overlay.CurrentBlur = MathHelper.Lerp(overlay.CurrentBlur, targetBlur, frameTime * rampSpeed);
            }

            Dirty(overlayUid, overlay);
        }
    }

    /// <summary>
    /// Starts a seizure process, beginning with prodrome warning phase.
    /// Duration is randomized from component's SeizureDuration range (min, max).
    /// </summary>
    public void StartSeizure(EntityUid uid, float? seizureDuration = null, float prodromeDuration = 10f)
    {
        if (HasComp<SeizureComponent>(uid))
            return; // Already having a seizure

        var seizureComp = AddComp<SeizureComponent>(uid);
        seizureComp.CurrentState = SeizureState.Prodrome;
        seizureComp.ProdromeDuration = prodromeDuration;
        seizureComp.RemainingTime = prodromeDuration;


        StartProdromePhase(uid, seizureComp);
    }

    private void StartProdromePhase(EntityUid uid, SeizureComponent component)
    {
        // Add SeizureOverlayComponent
        var overlayComp = EnsureComp<SeizureOverlayComponent>(uid);

        overlayComp.VisualState = SeizureVisualState.Prodrome;
        overlayComp.BlurryMagnitude = 1.5f;
        overlayComp.CurrentBlur = overlayComp.CurrentBlur;
        overlayComp.PulseAmplitude = 0.3f;
        overlayComp.PulseFrequency = 0.8f;
        overlayComp.RampUpSpeed = 2f;
        overlayComp.RampDownSpeed = 1f;
        overlayComp.PulseAccumulator = (float)(DateTime.UtcNow.TimeOfDay.TotalSeconds % 1000.0);
        overlayComp.IsFading = false;
        overlayComp.UseSoftShader = false;
        overlayComp.Softness = 0.45f;
        overlayComp.FadeOutDuration = 1f;
        Dirty(uid, overlayComp);

        // Initialize movement speed multiplier for smooth progressive slowdown
        component.MovementSpeedMultiplier = 1.0f;
        component.TargetMovementSpeed = 1.0f;


        // Show prodrome warning message
        try
        {
            _popup.PopupEntity(Loc.GetString("blimpuf-seizure-prodrome-self"), uid, uid, PopupType.LargeCaution);
            var othersMessage = Loc.GetString("blimpuf-seizure-prodrome-others", ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString("blimpuf-seizure-prodrome-self"), othersMessage, uid, uid, PopupType.LargeCaution);
        }
        catch
        {
            // do nothing cause whatevrurrrrr
        }
    }

    private void StartSeizurePhase(EntityUid uid, SeizureComponent component)
    {
        component.CurrentState = SeizureState.Seizure;

        var actualDuration = _random.NextFloat(component.SeizureDuration.X, component.SeizureDuration.Y);
        component.RemainingTime = actualDuration;

        // Modify SeizureOverlayComponent for harsh overlay with smooth transition
        if (TryComp<SeizureOverlayComponent>(uid, out var overlayComp))
        {
            overlayComp.VisualState = SeizureVisualState.Seizure;
            overlayComp.BlurryMagnitude = 5.5f;
            overlayComp.PulseAmplitude = 1.2f;
            overlayComp.PulseFrequency = 0.3f;
            overlayComp.RampUpSpeed = 3f;
            overlayComp.RampDownSpeed = 2f;
            overlayComp.IsFading = false;
            overlayComp.UseSoftShader = false;
            overlayComp.Softness = 0.45f;
            Dirty(uid, overlayComp);
        }

        // Apply stun and knockdown for the seizure duration
        _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(component.RemainingTime));
        _jittering.DoJitter(uid, TimeSpan.FromSeconds(component.RemainingTime), true,
            component.JitterAmplitude, component.JitterFrequency, true);

        // stuttering accent during seizure
        var stutterComp = EnsureComp<StutteringAccentComponent>(uid);
        stutterComp.MatchRandomProb = 0.9f;
        stutterComp.FourRandomProb = 0.3f;
        stutterComp.ThreeRandomProb = 0.4f;
        stutterComp.CutRandomProb = 0.1f;


        // Show seizure start message
        try
        {
            _popup.PopupEntity(Loc.GetString("blimpuf-seizure-self"), uid, uid, PopupType.LargeCaution);
            var othersMessage = Loc.GetString("blimpuf-seizure-others", ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString("blimpuf-seizure-self"), othersMessage, uid, uid, PopupType.LargeCaution);
        }
        catch
        {
            // do nothing cause whatevrurrrrr
        }
    }

    private void StartRecoveryPhase(EntityUid uid, SeizureComponent component)
    {
        component.CurrentState = SeizureState.Recovery;
        component.RemainingTime = component.RecoveryDuration;

        // Remove stuttering accent as recovery begins
        RemComp<StutteringAccentComponent>(uid);

        // Show recovery message
        try
        {
            _popup.PopupEntity(Loc.GetString("blimpuf-seizure-end-self"), uid, uid, PopupType.SmallCaution);
            var othersMessage = Loc.GetString("blimpuf-seizure-end-others", ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString("blimpuf-seizure-end-self"), othersMessage, uid, uid, PopupType.SmallCaution);
        }
        catch
        {
            // hi mom, we're doing nothing
        }
    }

    private void OnSeizureStart(EntityUid uid, SeizureComponent component, ComponentStartup args)
    {
        // Initialize
        component.MovementSpeedMultiplier = 1.0f;
        component.TargetMovementSpeed = 1.0f;

        // Refresh speed modifiers
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSeizureEnd(EntityUid uid, SeizureComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Remove stuttering accent if still present
        RemComp<StutteringAccentComponent>(uid);
    }

    private void UpdateMovementSpeed(EntityUid uid, SeizureComponent seizure, float frameTime)
    {
        if (seizure.CurrentState == SeizureState.Prodrome)
        {
            // During prodrome, gradually slow down as seizure approaches
            // Calculate how much time has passed in the prodrome phase
            var prodromeProgress = 1.0f - (seizure.RemainingTime / seizure.ProdromeDuration);

            seizure.MovementSpeedMultiplier = MathHelper.Lerp(1.0f, 0.6f, prodromeProgress);
        }
        else if (seizure.CurrentState == SeizureState.Seizure)
        {
            // During seizure, apply very slow movement
            var speedChangeRate = 3.0f;
            seizure.MovementSpeedMultiplier = MathHelper.Lerp(seizure.MovementSpeedMultiplier, 0.1f, frameTime * speedChangeRate);
        }
        else if (seizure.CurrentState == SeizureState.Recovery)
        {
            // During recovery, return to normal speed
            var speedChangeRate = 0.5f;
            seizure.MovementSpeedMultiplier = MathHelper.Lerp(seizure.MovementSpeedMultiplier, 1.0f, frameTime * speedChangeRate);
        }

        // Apply the movement speed modifier
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, SeizureComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        // Apply the current movement speed multiplier from the seizure
        args.ModifySpeed(component.MovementSpeedMultiplier, component.MovementSpeedMultiplier);
    }

    private void OnOverlayRefreshMovementSpeed(EntityUid uid, SeizureOverlayComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.HandleMovementRecovery)
        {
            args.ModifySpeed(component.MovementSpeedMultiplier, component.MovementSpeedMultiplier);
        }
    }

    /// <summary>
    /// Checks if an entity is currently having a seizure or in prodrome.
    /// </summary>
    public bool IsSeizing(EntityUid uid)
    {
        return HasComp<SeizureComponent>(uid);
    }

    /// <summary>
    /// Checks if an entity is in the warning phase.
    /// </summary>
    public bool IsInProdrome(EntityUid uid)
    {
        if (TryComp<SeizureComponent>(uid, out var seizure))
            return seizure.CurrentState == SeizureState.Prodrome;
        return false;
    }

    /// <summary>
    /// Checks if an entity is in the active seizure phase.
    /// </summary>
    public bool IsInActiveSeizure(EntityUid uid)
    {
        if (TryComp<SeizureComponent>(uid, out var seizure))
            return seizure.CurrentState == SeizureState.Seizure;
        return false;
    }

    /// <summary>
    /// Stops a seizure immediately if one is in progress.
    /// </summary>
    public void StopSeizure(EntityUid uid)
    {
        if (HasComp<SeizureComponent>(uid))
        {
            // Clean up because we're good programmers that clean when we're done
            RemComp<StutteringAccentComponent>(uid);
            RemComp<SeizureComponent>(uid);
        }
    }
}
