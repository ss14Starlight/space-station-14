using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Electrocution;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Player;
using StunStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Electrocution;

public sealed partial class ElectrocutionSystem
{
    [Dependency] private StunStatusEffectsSystem _gloveStunStatus = default!;

    /// <summary>
    /// Replicates electrocution knockdown without dropping items.
    /// </summary>
    public override bool TryDoGloveElectrocution(
        EntityUid uid, EntityUid? sourceUid, int shockDamage, TimeSpan time, TimeSpan knockdownDuration, bool refresh,
        float siemensCoefficient = 1f, StatusEffectsComponent? statusEffects = null, bool ignoreInsulation = false)
    {
        if (!DoCommonElectrocutionAttempt(uid, sourceUid, ref siemensCoefficient, ignoreInsulation) ||
            siemensCoefficient <= 0)
            return false;

        var damage = (int) (shockDamage * siemensCoefficient);
        if (damage <= 0 ||
            !Resolve(uid, ref statusEffects, false) ||
            !_statusEffects.CanApplyEffect(uid, StatusKeyIn, statusEffects) ||
            !_statusEffects.TryAddStatusEffect<ElectrocutedComponent>(uid, StatusKeyIn, time, refresh, statusEffects))
            return false;

        if (siemensCoefficient > 0.5f)
        {
            // Apply the stun status directly because the stun helper drops held items.
            _gloveStunStatus.TryUpdateStatusEffectDuration(uid, SharedStunSystem.StunId, knockdownDuration);
            if (HasComp<CrawlerComponent>(uid))
            {
                // This is electrocution, so it is not stopped by knockdown immunity.
                _stun.TryKnockdown(uid, knockdownDuration, refresh, drop: false, force: true);
            }
        }

        if (_damageable.TryChangeDamage(uid, new DamageSpecifier(_prototypeManager.Index(DamageType), damage),
                out var actualDamage, origin: sourceUid))
        {
            _adminLogger.Add(LogType.Electrocution,
                $"{ToPrettyString(uid):entity} received {actualDamage:damage} powered electrocution damage{(sourceUid != null ? " from " + ToPrettyString(sourceUid.Value) : ""):source}");
        }

        _stuttering.DoStutter(uid, time * StutteringTimeMultiplier, refresh);
        _jittering.DoJitter(uid, time * JitterTimeMultiplier, refresh, JitterAmplitude, JitterFrequency, true, statusEffects);

        _popup.PopupEntity(Loc.GetString("electrocuted-component-mob-shocked-popup-player"), uid, uid);

        var filter = Filter.PvsExcept(uid, entityManager: EntityManager);
        var identifiedUid = Identity.Entity(uid, ent: EntityManager);
        if (sourceUid != null)
        {
            _popup.PopupEntity(Loc.GetString("electrocuted-component-mob-shocked-by-source-popup-others",
                ("mob", identifiedUid), ("source", sourceUid.Value)), uid, filter, true);
            PlayElectrocutionSound(uid, sourceUid.Value);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("electrocuted-component-mob-shocked-popup-others",
                ("mob", identifiedUid)), uid, filter, true);
        }

        RaiseLocalEvent(uid, new ElectrocutedEvent(uid, sourceUid, siemensCoefficient), true);
        return true;
    }
}
