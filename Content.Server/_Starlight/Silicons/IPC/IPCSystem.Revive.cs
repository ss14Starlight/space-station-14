// IPC Revive System - Server
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Server.EUI;
using Content.Server.Electrocution;
using Content.Server.Ghost;
using Content.Shared._Starlight.Silicons.IPC.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.PowerCell.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Silicons.IPC;

public sealed partial class IPCSystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!; // _STARLIGHT: For dangerous defib interaction
    
    /// <summary>
    /// Sets up event subscriptions for IPC revival/reboot mechanics.
    /// </summary>
    protected override void SetupRevive()
    {
        base.SetupRevive();

        SubscribeLocalEvent<IPCReviveComponent, TargetBeforeDefibrillatorZapsEvent>(OnBeforeZap);
        SubscribeLocalEvent<IPCReviveComponent, IPCRebootDoAfterEvent>(OnReviveDoAfter);
        SubscribeLocalEvent<IPCReviveComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<IPCReviveComponent, MobStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<IPCReviveComponent, GetVerbsEvent<Verb>>(AddReviveVerbs);
    }

    /// <summary>
    /// Handles completion of the IPC reboot do-after.
    /// Called when someone successfully completes the reboot process.
    /// </summary>
    private void OnReviveDoAfter(Entity<IPCReviveComponent> ent, ref IPCRebootDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        FinishReboot(ent);   
    }

    /// <summary>
    /// _STARLIGHT: Makes defibrillators dangerous to use on IPCs.
    /// Deals shock damage to the IPC and electrocutes the user.
    /// This prevents defibs from being used on IPCs (use reboot instead).
    /// </summary>
    private void OnBeforeZap(Entity<IPCReviveComponent> ent, ref TargetBeforeDefibrillatorZapsEvent args)
    {
        if (args.Cancelled ||
            !TryComp<DefibrillatorComponent>(args.Defib, out var defib))
            return;

        // Deal shock damage to the IPC (damages them instead of helping)
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.DefibShockDamage);
        
        // Electrocute the user
        _electrocution.TryDoElectrocution(args.EntityUsingDefib, null, ent.Comp.DefibElectrocutionDamage, ent.Comp.DefibElectrocutionTime, true);
        
        if (ent.Comp.DefibBatteryDrain)
            DrainBattery(ent.Owner);

        _audio.PlayPvs(defib.ZapSound, args.Defib);
        args.Cancel(); // Cancel the normal defib effect
    }

    private void AddReviveVerbs(Entity<IPCReviveComponent> ent, ref GetVerbsEvent<Verb> ev)
    {
        if (!ev.CanInteract || !ev.CanAccess || !ev.CanComplexInteract ||
            !ent.Comp.RebootButton)
            return;
        
        // Only show reboot option when IPC is dead or critical
        if (!_state.IsDead(ent) && !_state.IsCritical(ent))
            return;

        var verb = new Verb
        {
            Text = Loc.GetString(ent.Comp.RebootButtonLabel),
            Category = new(ent.Comp.RebootButtonSubmenuLabel, ent.Comp.RebootButtonSubmenuIcon),
            Icon = new SpriteSpecifier.Texture(new ResPath(ent.Comp.RebootButtonIcon)),
            Act = () => StartReboot(ent),
        };

        ev.Verbs.Add(verb);
        
    }

    public void StartReboot(Entity<IPCReviveComponent> ent)
    {
        if (!ent.Comp.RebootButton)
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageableComponent) ||
            !_mobThreshold.TryGetThresholdForState(ent, MobState.Dead, out var thresholdDead) ||
            damageableComponent.TotalDamage > thresholdDead || 
            !CheckBatteryHasCharge(ent))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.CantReviveMessage), ent);
            _audio.PlayPvs(ent.Comp.RebootFailSound, ent);
            return;
        }
        
        _popup.PopupEntity(Loc.GetString(ent.Comp.RebootingMessage), ent);
        _audio.PlayPvs(ent.Comp.RebootSound, ent);

        if (ent.Comp.RebootTime == TimeSpan.Zero)
            FinishReboot(ent);
        else
        {
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.RebootTime, new IPCRebootDoAfterEvent(), ent)
            {
                Hidden = false,
                NeedHand = false,
                BreakOnMove = true,
                BreakOnWeightlessMove = true,
                BreakOnDamage = true,
                CancelDuplicate = true,
                RequireCanInteract = false,
            });
        }
    }

    private void FinishReboot(Entity<IPCReviveComponent> ent)
    {
        var dead = false;
        var hasPlayer = false;

        // No Unrevivable check - IPCs use their own reboot system, not biological revival

        if (TryComp<DamageableComponent>(ent, out var damageableComponent) &&
            _mobThreshold.TryGetThresholdForState(ent, MobState.Dead, out var thresholdDead) &&
            _mobThreshold.TryGetThresholdForState(ent, MobState.Critical, out var thresholdCrit))
        {
            if (damageableComponent.TotalDamage < thresholdCrit)
                _state.ChangeMobState(ent, MobState.Alive);
            else if (damageableComponent.TotalDamage < thresholdDead)
                _state.ChangeMobState(ent, MobState.Critical);
        } else
            dead = true;

        if (_mind.TryGetMind(ent, out _, out var mind) &&
            _player.TryGetSessionById(mind.UserId, out var playerSession))
        {
            hasPlayer = true;

            if (mind.CurrentEntity != ent)
            {
                _euiManager.OpenEui(new ReturnToBodyEui(mind, _mind, _player), playerSession);
            }
        }

        var sound = dead || !hasPlayer
            ? ent.Comp.RebootFailSound
            : ent.Comp.RebootSuccessSound;
        _audio.PlayPvs(sound, ent);
    }

    private void OnDamageChanged(Entity<IPCReviveComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.DamageSoundEnt != null && !IsDamaged(ent, args.Damageable))
        {
            _audio.Stop(ent.Comp.DamageSoundEnt);
            ent.Comp.DamageSoundEnt = null;
        } else if (ent.Comp.DamageSoundEnt == null && IsDamaged(ent, args.Damageable) && !_state.IsDead(ent))
        {
            if (TryComp<IPCBatteryComponent>(ent, out _))
                ent.Comp.DamageSoundEnt = _audio.PlayPvs(ent.Comp.DamagedSound, ent);
        }
    }

    private void OnStateChanged(Entity<IPCReviveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            // Stop damaged sound if playing
            if (ent.Comp.DamageSoundEnt != null)
            {
                _audio.Stop(ent.Comp.DamageSoundEnt);
                ent.Comp.DamageSoundEnt = null;
            }
            
            // Death message is now handled by DeathgaspComponent with IPCDeathgasp emote
        }
    }

    public bool IsDamaged(Entity<IPCReviveComponent> ent, DamageableComponent? damageable)
    {
        if (ent.Comp.DamagedThreshold == null)
            return false;
            
        return Resolve(ent, ref damageable) && damageable.TotalDamage >= ent.Comp.DamagedThreshold.Min &&
            (ent.Comp.DamagedThreshold.Max == null || damageable.TotalDamage <= ent.Comp.DamagedThreshold.Max);
    }
    
    private bool CheckBatteryHasCharge(EntityUid ent)
    {
        if (!TryComp<IPCBatteryComponent>(ent, out var battery))
            return true; // If no battery component, don't block revive

        return _powerCell.HasDrawCharge((ent, CompOrNull<PowerCellDrawComponent>(ent), battery.PowerCellSlot));
    }
}

