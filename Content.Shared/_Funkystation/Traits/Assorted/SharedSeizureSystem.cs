// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Movement.Systems;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Shared system that handles seizure effects including prodrome warning phase
/// and seizure with visual effects and movement impairment.
/// </summary>
public abstract class SharedSeizureSystem : EntitySystem
{
    [Dependency] protected readonly MovementSpeedModifierSystem MovementSpeed = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;

    // Movement speed constants
    private const float ProdromeMinSpeed = 0.6f;
    private const float SeizureSpeed = 0.1f;
    private const float SeizureSpeedChangeRate = 3.0f;
    private const float RecoverySpeedChangeRate = 0.5f;
    private const float OverlayRecoverySpeedChangeRate = 0.3f;

    // Fade constants
    private const float FadeThreshold = 0.01f;
    private const float DefaultFadeOutDuration = 1f;

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

        UpdateSeizureComponents(frameTime);
        UpdateOverlayComponents(frameTime);
    }

    protected virtual void UpdateSeizureComponents(float frameTime)
    {
        var query = EntityQueryEnumerator<SeizureComponent>();
        while (query.MoveNext(out var uid, out var seizure))
        {
            seizure.RemainingTime -= frameTime;

            UpdateMovementSpeed(uid, seizure, frameTime);
            UpdateOverlayVisuals(uid, seizure, frameTime);
            Dirty(uid, seizure);

            if (seizure.RemainingTime <= 0f)
            {
                HandlePhaseTransition(uid, seizure);
            }
        }
    }

    protected virtual void UpdateOverlayComponents(float frameTime)
    {
        var overlayQuery = EntityQueryEnumerator<SeizureOverlayComponent>();
        while (overlayQuery.MoveNext(out var overlayUid, out var overlay))
        {
            overlay.PulseAccumulator += frameTime;

            if (overlay.HandleMovementRecovery)
            {
                UpdateMovementRecovery(overlayUid, overlay, frameTime);
            }

            UpdateOverlayFade(overlayUid, overlay, frameTime);
            Dirty(overlayUid, overlay);
        }
    }

    protected virtual void UpdateOverlayVisuals(EntityUid uid, SeizureComponent seizure, float frameTime)
    {
        if (!TryComp<SeizureOverlayComponent>(uid, out var overlayComp))
            return;

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
                return;
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

    protected virtual void UpdateMovementRecovery(EntityUid uid, SeizureOverlayComponent overlay, float frameTime)
    {
        overlay.MovementSpeedMultiplier = MathHelper.Lerp(
            overlay.MovementSpeedMultiplier,
            1.0f,
            frameTime * OverlayRecoverySpeedChangeRate);

        MovementSpeed.RefreshMovementSpeedModifiers(uid);

        if (MathF.Abs(overlay.MovementSpeedMultiplier - 1.0f) < FadeThreshold)
        {
            overlay.MovementSpeedMultiplier = 1.0f;
            overlay.HandleMovementRecovery = false;
            MovementSpeed.RefreshMovementSpeedModifiers(uid);
        }
    }

    protected virtual void UpdateOverlayFade(EntityUid uid, SeizureOverlayComponent overlay, float frameTime)
    {
        if (overlay.IsFading)
        {
            UpdateFadeValues(overlay, frameTime);

            if (IsFadeComplete(overlay))
            {
                RemComp<SeizureOverlayComponent>(uid);
            }
        }
        else
        {
            UpdateBlurInterpolation(overlay, frameTime);
        }
    }

    private static void UpdateFadeValues(SeizureOverlayComponent overlay, float frameTime)
    {
        var fadeSpeed = 1.0f / overlay.FadeOutDuration;
        var fadeAmount = fadeSpeed * frameTime;

        overlay.BlurryMagnitude = MathHelper.Lerp(overlay.BlurryMagnitude, 0f, fadeAmount);
        overlay.PulseAmplitude = MathHelper.Lerp(overlay.PulseAmplitude, 0f, fadeAmount);
        overlay.CurrentBlur = MathHelper.Lerp(overlay.CurrentBlur, 0f, fadeAmount);
    }

    private static bool IsFadeComplete(SeizureOverlayComponent overlay)
    {
        return overlay.BlurryMagnitude <= FadeThreshold
               && overlay.PulseAmplitude <= FadeThreshold
               && overlay.CurrentBlur <= FadeThreshold;
    }

    private static void UpdateBlurInterpolation(SeizureOverlayComponent overlay, float frameTime)
    {
        var targetBlur = overlay.BlurryMagnitude;
        var rampSpeed = targetBlur > overlay.CurrentBlur
            ? overlay.RampUpSpeed
            : overlay.RampDownSpeed;

        overlay.CurrentBlur = MathHelper.Lerp(overlay.CurrentBlur, targetBlur, frameTime * rampSpeed);
    }

    protected virtual void HandlePhaseTransition(EntityUid uid, SeizureComponent seizure)
    {
        switch (seizure.CurrentState)
        {
            case SeizureState.Prodrome:
                TransitionToSeizure(uid, seizure);
                break;
            case SeizureState.Seizure:
                TransitionToRecovery(uid, seizure);
                break;
            case SeizureState.Recovery:
                TransitionToFading(uid, seizure);
                break;
            case SeizureState.Fading:
                RemComp<SeizureComponent>(uid);
                break;
        }
    }

    protected virtual void TransitionToSeizure(EntityUid uid, SeizureComponent seizure)
    {
        seizure.CurrentState = SeizureState.Seizure;
        var actualDuration = Random.NextFloat(seizure.SeizureDuration.X, seizure.SeizureDuration.Y);
        seizure.RemainingTime = actualDuration;
    }

    protected virtual void TransitionToRecovery(EntityUid uid, SeizureComponent seizure)
    {
        seizure.CurrentState = SeizureState.Recovery;
        seizure.RemainingTime = seizure.RecoveryDuration;
    }

    protected virtual void TransitionToFading(EntityUid uid, SeizureComponent seizure)
    {
        if (TryComp<SeizureOverlayComponent>(uid, out var overlay))
        {
            overlay.IsFading = true;
            overlay.FadeOutDuration = DefaultFadeOutDuration;
            overlay.MovementSpeedMultiplier = seizure.MovementSpeedMultiplier;
            overlay.HandleMovementRecovery = true;

            seizure.CurrentState = SeizureState.Fading;
            seizure.RemainingTime = overlay.FadeOutDuration;

            Dirty(uid, overlay);
        }
        else
        {
            RemComp<SeizureComponent>(uid);
        }
    }

    protected virtual void UpdateMovementSpeed(EntityUid uid, SeizureComponent seizure, float frameTime)
    {
        switch (seizure.CurrentState)
        {
            case SeizureState.Prodrome:
                // Gradually slow down during prodrome based on progress
                var prodromeProgress = 1.0f - (seizure.RemainingTime / seizure.ProdromeDuration);
                seizure.MovementSpeedMultiplier = MathHelper.Lerp(1.0f, ProdromeMinSpeed, prodromeProgress);
                break;

            case SeizureState.Seizure:
                // Rapidly drop to very slow movement during seizure
                seizure.MovementSpeedMultiplier = MathHelper.Lerp(
                    seizure.MovementSpeedMultiplier,
                    SeizureSpeed,
                    frameTime * SeizureSpeedChangeRate);
                break;

            case SeizureState.Recovery:
                // Gradually recover to normal speed
                seizure.MovementSpeedMultiplier = MathHelper.Lerp(
                    seizure.MovementSpeedMultiplier,
                    1.0f,
                    frameTime * RecoverySpeedChangeRate);
                break;
        }

        MovementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSeizureStart(EntityUid uid, SeizureComponent component, ComponentStartup args)
    {
        component.MovementSpeedMultiplier = 1.0f;
        component.TargetMovementSpeed = 1.0f;
        MovementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSeizureEnd(EntityUid uid, SeizureComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        component.MovementSpeedMultiplier = 1.0f;
        MovementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, SeizureComponent component, RefreshMovementSpeedModifiersEvent args)
    {
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
        return TryComp<SeizureComponent>(uid, out var seizure) && seizure.CurrentState == SeizureState.Prodrome;
    }

    /// <summary>
    /// Checks if an entity is in the active seizure phase.
    /// </summary>
    public bool IsInActiveSeizure(EntityUid uid)
    {
        return TryComp<SeizureComponent>(uid, out var seizure) && seizure.CurrentState == SeizureState.Seizure;
    }

    /// <summary>
    /// Starts a seizure on the target entity with the given parameters.
    /// This is the generic entry point for any system to trigger a seizure.
    /// </summary>
    /// <param name="uid">Target entity</param>
    /// <param name="prodromeDuration">Duration of prodrome (warning) phase in seconds</param>
    /// <param name="seizureDuration">Duration of seizure phase in seconds</param>
    /// <param name="recoveryDuration">Duration of recovery phase in seconds</param>
    /// <param name="overlay">If true, applies the seizure overlay effect</param>
    public virtual void StartSeizure(
        EntityUid uid,
        float prodromeDuration = 3f,
        float seizureDuration = 10f,
        float recoveryDuration = 5f,
        bool overlay = true)
    {
        // If already seizing, do nothing
        if (HasComp<SeizureComponent>(uid))
            return;

        var comp = EnsureComp<SeizureComponent>(uid);
        comp.CurrentState = SeizureState.Prodrome;
        comp.ProdromeDuration = prodromeDuration;
        comp.SeizureDuration = new System.Numerics.Vector2(seizureDuration, seizureDuration); // Use same value for min/max
        comp.RecoveryDuration = recoveryDuration;
        comp.RemainingTime = prodromeDuration;
        comp.MovementSpeedMultiplier = 1.0f;
        comp.TargetMovementSpeed = 1.0f;

        if (overlay)
        {
            var overlayComp = EnsureComp<SeizureOverlayComponent>(uid);
            overlayComp.IsFading = false;
            overlayComp.FadeOutDuration = 1f;
            overlayComp.BlurryMagnitude = 1f;
            overlayComp.PulseAmplitude = 1f;
            overlayComp.CurrentBlur = 0f;
            overlayComp.RampUpSpeed = 2f;
            overlayComp.RampDownSpeed = 1f;
            overlayComp.MovementSpeedMultiplier = 1.0f;
            overlayComp.HandleMovementRecovery = false;
        }
    }
}
