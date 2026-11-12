// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Server._Starlight.Speech.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.EntityEffects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server._Funkystation.Traits.Assorted;

/// <summary>
/// Server-side system that handles seizure game logic including
/// stunning, jittering, popups, and speech impairment.
/// </summary>
public sealed partial class SeizureSystem : SharedSeizureSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StutteringSystem _stuttering = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SeizureComponent, MoveEvent>(OnMoved);
    }

    private void OnMoved(Entity<SeizureComponent> ent, ref MoveEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var dragging = _standing.IsDown(ent.Owner);
        if (!dragging)
            return;

        if (TryComp<DamageableComponent>(ent, out var damage) && damage.TotalDamage >= ent.Comp.DamageUpperBound)
            return;

        var factor = (args.NewPosition.Position - args.OldPosition.Position).Length();
        // kazne made me hardcode damage
        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Blunt", 0.6f);
        var normalDamage = dspec * factor;
        _damageable.TryChangeDamage(ent.Owner, normalDamage);
        _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> {ent}, Filter.Pvs(ent, entityManager: EntityManager));
    }
    protected override void UpdateSeizureComponents(float frameTime)
    {
        var query = EntityQueryEnumerator<SeizureComponent>();
        while (query.MoveNext(out var uid, out var seizure))
        {
            // Dead entities can't have seizures
            if (_mobState.IsDead(uid))
                continue;

            seizure.RemainingTime -= frameTime;

            UpdateMovementSpeed(uid, seizure, frameTime);
            UpdateOverlayVisuals(uid, seizure, frameTime);
            Dirty(uid, seizure);

            if (seizure.RemainingTime <= 0f)
            {
                HandlePhaseTransition(uid, seizure);
                continue;
            }
        }
    }

    protected override void UpdateOverlayComponents(float frameTime)
    {
        var overlayQuery = EntityQueryEnumerator<SeizureOverlayComponent>();
        while (overlayQuery.MoveNext(out var overlayUid, out var overlay))
        {
            // Dead entities can't have seizures
            if (_mobState.IsDead(overlayUid))
                continue;

            overlay.PulseAccumulator += frameTime;

            if (overlay.HandleMovementRecovery)
            {
                UpdateMovementRecovery(overlayUid, overlay, frameTime);
            }

            UpdateOverlayFade(overlayUid, overlay, frameTime);
            Dirty(overlayUid, overlay);
        }
    }

    /// <summary>
    /// Starts a seizure process, beginning with prodrome warning phase.
    /// </summary>
    public void StartSeizure(EntityUid uid, float? seizureDuration = null, float prodromeDuration = 10f,
        string? prodromePopupKey = null, string? seizurePopupKey = null, string? recoveryPopupKey = null)
    {
        if (HasComp<SeizureComponent>(uid))
            return; // Already having a seizure

        var seizureComp = AddComp<SeizureComponent>(uid);
        seizureComp.CurrentState = SeizureState.Prodrome;
        seizureComp.ProdromeDuration = prodromeDuration;
        seizureComp.RemainingTime = prodromeDuration;

        if (seizureDuration is { } duration)
            seizureComp.SeizureDuration = new(duration, duration);

        seizureComp.ProdromePopupKey = prodromePopupKey;
        seizureComp.SeizurePopupKey = seizurePopupKey;
        seizureComp.RecoveryPopupKey = recoveryPopupKey;

        StartProdromePhase(uid, seizureComp);
    }

    private void StartProdromePhase(EntityUid uid, SeizureComponent component)
    {
        var overlayComp = EnsureComp<SeizureOverlayComponent>(uid);

        overlayComp.VisualState = SeizureVisualState.Prodrome;
        overlayComp.BlurryMagnitude = 1.5f;
        overlayComp.PulseAmplitude = 0.3f;
        overlayComp.PulseFrequency = 0.8f;
        overlayComp.RampUpSpeed = 2f;
        overlayComp.RampDownSpeed = 1f;
        overlayComp.PulseAccumulator = 0f;
        overlayComp.IsFading = false;
        overlayComp.UseSoftShader = false;
        overlayComp.Softness = 0.45f;
        overlayComp.FadeOutDuration = 1f;
        Dirty(uid, overlayComp);

        component.MovementSpeedMultiplier = 1.0f;
        component.TargetMovementSpeed = 1.0f;

        ShowProdromePopup(uid, component.ProdromePopupKey);
    }

    protected override void TransitionToSeizure(EntityUid uid, SeizureComponent seizure)
    {
        base.TransitionToSeizure(uid, seizure);

        // Update visual overlay for harsh seizure effects
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

        // Apply server-side effects
        _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(seizure.RemainingTime));
        _jittering.DoJitter(uid, TimeSpan.FromSeconds(seizure.RemainingTime), true,
            seizure.JitterAmplitude, seizure.JitterFrequency, true);
        _stuttering.DoStutter(uid, TimeSpan.FromSeconds(seizure.RemainingTime), true);

        ShowSeizurePopup(uid, seizure.SeizurePopupKey);
    }

    protected override void TransitionToRecovery(EntityUid uid, SeizureComponent seizure)
    {
        base.TransitionToRecovery(uid, seizure);
        ShowRecoveryPopup(uid, seizure.RecoveryPopupKey);
    }

    private void ShowProdromePopup(EntityUid uid, string? popupKey)
    {
        var selfKey = popupKey ?? "blimpuf-seizure-prodrome-self";
        var othersKey = popupKey != null ? popupKey + "-others" : "blimpuf-seizure-prodrome-others";
        try
        {
            _popup.PopupEntity(Loc.GetString(selfKey), uid, uid, PopupType.LargeCaution);
            var othersMessage = Loc.GetString(othersKey, ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString(selfKey), othersMessage, uid, uid, PopupType.LargeCaution);
        }
        catch
        {
            // Silently fail if localization is missing
        }
    }

    private void ShowSeizurePopup(EntityUid uid, string? popupKey)
    {
        var selfKey = popupKey ?? "blimpuf-seizure-self";
        var othersKey = popupKey != null ? popupKey + "-others" : "blimpuf-seizure-others";
        try
        {
            _popup.PopupEntity(Loc.GetString(selfKey), uid, uid, PopupType.LargeCaution);
            var othersMessage = Loc.GetString(othersKey, ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString(selfKey), othersMessage, uid, uid, PopupType.LargeCaution);
        }
        catch
        {
            // Silently fail if localization is missing
        }
    }

    private void ShowRecoveryPopup(EntityUid uid, string? popupKey)
    {
        var selfKey = popupKey ?? "blimpuf-seizure-end-self";
        var othersKey = popupKey != null ? popupKey + "-others" : "blimpuf-seizure-end-others";
        try
        {
            _popup.PopupEntity(Loc.GetString(selfKey), uid, uid, PopupType.SmallCaution);
            var othersMessage = Loc.GetString(othersKey, ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupPredicted(Loc.GetString(selfKey), othersMessage, uid, uid, PopupType.SmallCaution);
        }
        catch
        {
            // Silently fail if localization is missing
        }
    }

    /// <summary>
    /// Stops a seizure immediately if one is in progress.
    /// </summary>
    public void StopSeizure(EntityUid uid)
    {
        if (!HasComp<SeizureComponent>(uid))
            return;

        _stuttering.DoRemoveStutter(uid);
        RemComp<SeizureComponent>(uid);
    }

    /// <summary>
    /// Triggers a seizure on the target entity.
    /// </summary>
    public sealed partial class TriggerSeizureEffect : EntityEffectBase<TriggerSeizureEffect>
    {
        /// <summary>
        /// Optional custom seizure duration (seconds).
        /// </summary>
        [DataField("seizureDuration")]
        public float? SeizureDuration;

        /// <summary>
        /// Optional custom prodrome duration (seconds).
        /// </summary>
        [DataField("prodromeDuration")]
        public float ProdromeDuration = 10f;

        public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc)
            => null;
    }
}

public sealed partial class TriggerSeizureEntityEffectSystem : EntityEffectSystem<NeuroAversionComponent, SeizureSystem.TriggerSeizureEffect>
{
    protected override void Effect(Entity<NeuroAversionComponent> entity, ref EntityEffectEvent<SeizureSystem.TriggerSeizureEffect> args)
    {
        var duration = args.Effect.SeizureDuration.HasValue
            ? TimeSpan.FromSeconds(args.Effect.SeizureDuration.Value)
            : entity.Comp.NeuroAversionSeizureDuration;
        EntityManager.System<SeizureSystem>().StartSeizure(entity, (float)duration.TotalSeconds, args.Effect.ProdromeDuration);
        entity.Comp.SeizureBuild = entity.Comp.PostSeizureResidual;
        Dirty(entity);
    }
}
