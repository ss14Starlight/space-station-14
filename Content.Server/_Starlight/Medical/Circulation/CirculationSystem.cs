// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Body.Components;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Medical.Circulation;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Circulation;

/// <summary>
/// Simulates blood circulation: heart output and blood volume give perfusion, the lungs give the blood's oxygen
/// saturation, and tissues starved of oxygen take hypoxia damage.
/// </summary>
public sealed partial class CirculationSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<CirculationComponent> ent, ref MapInitEvent args)
        => ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;

    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<CirculationComponent> ent, ref MobStateChangedEvent args)
    {
        if (GetHeart(ent) is not { } heart)
            return;

        // The heart stops with death and starts again with a revival.
        if (args.NewMobState == MobState.Dead)
            SetRhythm(heart, HeartRhythm.Arrest);
        else if (args.OldMobState == MobState.Dead)
            SetRhythm(heart, HeartRhythm.Normal);
    }

    [SubscribeLocalEvent]
    private void OnApplyMetabolicMultiplier(Entity<CirculationComponent> ent, ref ApplyMetabolicMultiplierEvent args)
        => ent.Comp.UpdateIntervalMultiplier = args.Multiplier;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CirculationComponent>();
        while (query.MoveNext(out var uid, out var circulation))
        {
            if (now < circulation.NextUpdate)
                continue;
            circulation.NextUpdate = now + circulation.UpdateInterval * circulation.UpdateIntervalMultiplier;

            // Bodies built without a heart (IPC and the like) do not circulate blood. Checked on the first update,
            // once the body has been built.
            if (!circulation.Checked)
            {
                circulation.Checked = true;
                circulation.HasCirculation = !circulation.Disabled && GetHeart(uid) != null;
                Dirty(uid, circulation);
            }

            if (!circulation.HasCirculation)
                continue;

            UpdateCirculation((uid, circulation));
        }
    }

    private void UpdateCirculation(Entity<CirculationComponent> ent)
    {
        var circulation = ent.Comp;
        var heart = GetHeart(ent);

        var bloodLevel = TryComp<BloodstreamComponent>(ent, out var bloodstream)
            ? _bloodstream.GetBloodLevel((ent, bloodstream))
            : 1f;
        var volume = Math.Clamp((bloodLevel - circulation.NoPerfusionBloodLevel)
            / (circulation.FullPerfusionBloodLevel - circulation.NoPerfusionBloodLevel), 0f, 1f);

        var output = heart is { } h ? GetHeartOutput(h) : 0f;
        circulation.Perfusion = output * volume;

        // The heart races to make up for lost blood.
        circulation.HeartRate = heart is { } beating && output > 0f
            ? beating.Comp.BaseRate * (1f + (1f - volume) * circulation.MaxCompensation)
            : 0f;

        circulation.SpO2 += (GetLungOxygenation(ent) - circulation.SpO2) * circulation.SpO2Response;
        circulation.OxygenDelivery = circulation.SpO2 * circulation.Perfusion;
        Dirty(ent);

        if (_mobState.IsDead(ent))
            return;

        var seconds = (float) circulation.UpdateInterval.TotalSeconds;
        if (circulation.OxygenDelivery < circulation.HypoxiaThreshold)
        {
            var severity = (circulation.HypoxiaThreshold - circulation.OxygenDelivery) / circulation.HypoxiaThreshold;
            _status.TrySetStatusEffectDuration(ent, SharedBloodstreamSystem.Bloodloss);

            // Tissues live on the oxygen they hold for a while (a stopped heart can be restarted, a heart swapped)
            // before they start to die.
            if (circulation.OxygenReserve > 0f)
            {
                circulation.OxygenReserve = MathF.Max(0f, circulation.OxygenReserve - seconds * severity);
                return;
            }

            _damageable.TryChangeDamage(ent.Owner, circulation.HypoxiaDamage * severity, ignoreResistances: true, interruptsDoAfters: false);
        }
        else
        {
            if (circulation.OxygenDelivery > circulation.RecoveryThreshold)
            {
                circulation.OxygenReserve = MathF.Min(circulation.MaxOxygenReserve, circulation.OxygenReserve + seconds);
                _damageable.TryChangeDamage(ent.Owner, circulation.RecoveryDamage, ignoreResistances: true, interruptsDoAfters: false);
            }
            _status.TryRemoveStatusEffect(ent, SharedBloodstreamSystem.Bloodloss);
        }
    }

    /// <summary>
    /// How much of a healthy heart's output the heart gives, 0..1.
    /// </summary>
    public float GetHeartOutput(Entity<HeartComponent> heart)
    {
        var rhythm = heart.Comp.Rhythm switch
        {
            HeartRhythm.Normal => 1f,
            HeartRhythm.Arrhythmia => 0.7f,
            HeartRhythm.Fibrillation => 0.1f,
            _ => 0f,
        };

        var damage = TryComp<DamageableComponent>(heart, out var damageable)
            ? _damageable.GetTotalDamage((heart, damageable)).Float()
            : 0f;
        var integrity = heart.Comp.MaxDamage > 0 ? Math.Clamp(1f - damage / heart.Comp.MaxDamage.Float(), 0f, 1f) : 1f;

        return rhythm * integrity;
    }

    /// <summary>
    /// Oxygen saturation the lungs bring the blood to, from how much air the body has been getting.
    /// </summary>
    private float GetLungOxygenation(EntityUid body)
    {
        if (!TryComp<RespiratorComponent>(body, out var respirator))
            return 0.98f;

        var saturation = respirator.Saturation;
        var threshold = respirator.SuffocationThreshold;
        if (saturation >= threshold)
        {
            var range = Math.Max(0.01f, respirator.MaxSaturation - threshold);
            return 0.88f + 0.1f * Math.Clamp((saturation - threshold) / range, 0f, 1f);
        }

        var low = Math.Max(0.01f, threshold - respirator.MinSaturation);
        return 0.5f + 0.38f * Math.Clamp((saturation - respirator.MinSaturation) / low, 0f, 1f);
    }

    public void SetRhythm(Entity<HeartComponent> heart, HeartRhythm rhythm)
    {
        if (heart.Comp.Rhythm == rhythm)
            return;

        heart.Comp.Rhythm = rhythm;
        Dirty(heart);
    }

    public Entity<HeartComponent>? GetHeart(EntityUid body)
    {
        foreach (var heart in _body.GetBodyOrganEntityComps<HeartComponent>((body, null)))
            return (heart.Owner, heart.Comp1);
        return null;
    }
}
