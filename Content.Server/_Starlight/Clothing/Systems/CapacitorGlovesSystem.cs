using Content.Server._Starlight.Clothing.Components;
using Content.Server.DoAfter;
using Content.Server.Ninja.Systems;
using Content.Server.Power.Components;
using Content.Shared._Starlight.Clothing;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Clothing.Systems;

/// <summary>
/// Handles CE capacitor gloves:
/// <list type="bullet">
/// <item><b>Drain</b>: hand-interact with an APC/SMES/substation to charge the slotted cell.</item>
/// <item><b>Inject</b>: activation verb on a power device pushes charge from the cell into it.</item>
/// <item><b>Supermatter immunity</b> while worn.</item>
/// <item><b>EMP vulnerability</b>: EMP pulses drain the slotted cell entirely.</item>
/// </list>
/// </summary>
public sealed partial class CapacitorGlovesSystem : EntitySystem
{
    [Dependency] private SharedBatteryDrainerSystem _drainer = default!;
    [Dependency] private BatteryDrainerSystem _drainerServer = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CapacitorGlovesComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CapacitorGlovesComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CapacitorGlovesComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<CapacitorGlovesComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CapacitorGlovesComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<PowerNetworkBatteryComponent, GetVerbsEvent<AlternativeVerb>>(OnGetGlovesVerbs);
        SubscribeLocalEvent<CapacitorGlovesComponent, CapacitorInjectDoAfterEvent>(OnInjectDoAfter);

        SubscribeLocalEvent<WornCapacitorGlovesComponent, EmpPulseEvent>(OnWearerEmp);
    }

    // ─────────────────────────── Mode toggle ───────────────────────────────

    private void OnActivate(Entity<CapacitorGlovesComponent> ent, ref ActivateInWorldEvent args)
    {
        args.Handled = true;
        var comp = ent.Comp;
        comp.Mode = comp.Mode == CapacitorGlovesMode.Drain
            ? CapacitorGlovesMode.Inject
            : CapacitorGlovesMode.Drain;

        // Enable / disable the battery drainer to match the new mode.
        if (comp.DrainerTarget is { } drainer)
            _drainer.SetHandInteractionEnabled(drainer, false);

        var modeStr = comp.Mode == CapacitorGlovesMode.Drain
            ? Loc.GetString("capacitor-gloves-mode-drain")
            : Loc.GetString("capacitor-gloves-mode-inject");
        _popup.PopupEntity(Loc.GetString("capacitor-gloves-mode-switched", ("mode", modeStr)), ent.Owner, args.User);
    }

    private void OnExamined(Entity<CapacitorGlovesComponent> ent, ref ExaminedEvent args)
    {
        var modeStr = ent.Comp.Mode == CapacitorGlovesMode.Drain
            ? Loc.GetString("capacitor-gloves-mode-drain")
            : Loc.GetString("capacitor-gloves-mode-inject");
        args.PushMarkup(Loc.GetString("capacitor-gloves-examine-mode", ("mode", modeStr)));
    }

    // ───────────────────────────── Equip / Unequip ─────────────────────────────

    private void OnEquipped(Entity<CapacitorGlovesComponent> ent, ref GotEquippedEvent args)
    {
        var wearer = args.EquipTarget;
        var comp = ent.Comp;

        comp.WearerUid = wearer;

        var worn = EnsureComp<WornCapacitorGlovesComponent>(wearer);
        worn.GlovesUid = ent.Owner;

        if (!HasComp<BatteryDrainerComponent>(wearer))
        {
            EnsureComp<BatteryDrainerComponent>(wearer);
            _drainer.SetDrainConfig(wearer, comp.DrainEfficiency, comp.DrainTime, comp.MaxDrainPerTick);
            comp.DrainerTarget = wearer;
        }

        // Honour whichever mode was saved on the component.
        if (comp.DrainerTarget is { } drainer)
        {
            _drainer.SetHandInteractionEnabled(drainer, false);
            UpdateBattery(ent, drainer);
        }
    }

    private void OnUnequipped(Entity<CapacitorGlovesComponent> ent, ref GotUnequippedEvent args)
    {
        var wearer = args.EquipTarget;
        var comp = ent.Comp;

        comp.WearerUid = null;
        comp.AutoInjectTarget = null;

        RemCompDeferred<WornCapacitorGlovesComponent>(wearer);

        if (comp.DrainerTarget != null)
        {
            RemComp<BatteryDrainerComponent>(comp.DrainerTarget.Value);
            comp.DrainerTarget = null;
        }
    }

    private void OnPowerCellChanged(Entity<CapacitorGlovesComponent> ent, ref PowerCellChangedEvent ev)
    {
        if (ent.Comp.DrainerTarget is { } target)
            UpdateBattery(ent, target);
    }

    private void UpdateBattery(Entity<CapacitorGlovesComponent> gloves, EntityUid drainerTarget)
    {
        if (_powerCell.TryGetBatteryFromSlot(gloves.Owner, out var battery))
            _drainer.SetBattery(drainerTarget, battery);
        else
            _drainer.SetBattery(drainerTarget, null);
    }

    // ─────────────────────────── Power verbs (drain & inject) ────────────────────

    private void OnGetGlovesVerbs(Entity<PowerNetworkBatteryComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_inventory.TryGetSlotEntity(args.User, "gloves", out var glovesEnt) ||
            !TryComp<CapacitorGlovesComponent>(glovesEnt, out var comp))
            return;

        var glovesRef = glovesEnt.Value;
        var targetRef = target.Owner;
        var user = args.User;

        if (comp.Mode == CapacitorGlovesMode.Drain)
        {
            if (comp.DrainerTarget is not {} drainerUid)
                return;

            if (!TryComp<BatteryDrainerComponent>(drainerUid, out var drainerComp))
                return;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("capacitor-gloves-drain-verb"),
                Priority = 100,
                Act = () => _drainerServer.TryStartDrain((drainerUid, drainerComp), targetRef)
            });
        }
        else // Inject mode
        {
            // Block injection while on post-inject cooldown.
            if (_timing.CurTime < comp.InjectAvailableAt)
                return;

            // If auto-inject is already running on this exact target, offer to stop it.
            if (comp.AutoInjectTarget == targetRef)
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("capacitor-gloves-inject-verb-stop"),
                    Priority = 100,
                    Act = () =>
                    {
                        if (TryComp<CapacitorGlovesComponent>(glovesRef, out var c))
                            c.AutoInjectTarget = null;
                        _popup.PopupEntity(Loc.GetString("capacitor-gloves-inject-stop"), glovesRef, user);
                    }
                });
                return;
            }

            if (!_powerCell.TryGetBatteryFromSlot(glovesRef, out var cellEnt))
                return;

            if (_battery.GetCharge(cellEnt.Value.AsNullable()) <= 0)
                return;

            if (TryComp<BatteryComponent>(targetRef, out var targetBat) &&
                _battery.IsFull(new Entity<BatteryComponent?>(targetRef, targetBat)))
                return;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("capacitor-gloves-inject-verb"),
                Priority = 100,
                Act = () =>
                {
                    if (!TryComp<CapacitorGlovesComponent>(glovesRef, out var c))
                        return;
                    c.AutoInjectTarget = targetRef;
                    _popup.PopupEntity(Loc.GetString("capacitor-gloves-inject-start", ("target", targetRef)), glovesRef, user);
                    StartInjectDoAfter(user, glovesRef, targetRef);
                }
            });
        }
    }

    /// <summary>
    /// Performs a single inject tick from the gloves cell into <paramref name="target"/>.
    /// </summary>
    /// <returns><c>true</c> when injection is complete (cell empty or target full).</returns>
    private bool DoSingleInjectTick(EntityUid user, EntityUid gloves, EntityUid target, CapacitorGlovesComponent comp)
    {
        if (!_powerCell.TryGetBatteryFromSlot(gloves, out var cellEnt))
            return true;

        if (!TryComp<BatteryComponent>(target, out var targetBat))
            return true;

        var available = _battery.GetCharge(cellEnt.Value.AsNullable());
        if (available <= 0)
            return true;

        var room = targetBat.MaxCharge - _battery.GetCharge(new Entity<BatteryComponent?>(target, targetBat));
        if (room <= 0)
            return true;

        var drawFromCell = Math.Min(available, room / comp.InjectionEfficiency);
        BluespaceCapacitorBatteryComponent? bsCell = null;
        if (TryComp(cellEnt.Value.Owner, out bsCell))
            drawFromCell = Math.Min(drawFromCell, bsCell.TransferRateLimit);
        else
            drawFromCell = Math.Min(drawFromCell, comp.InjectRateLimit);
        var putInTarget = drawFromCell * comp.InjectionEfficiency;

        if (!_battery.TryUseCharge(cellEnt.Value.AsNullable(), drawFromCell))
            return true;

        _battery.ChangeCharge(new Entity<BatteryComponent?>(target, targetBat), putInTarget);
        Spawn("EffectSparks", Transform(target).Coordinates);

        // Done when target is full or cell is now empty.
        var postRoom = targetBat.MaxCharge - _battery.GetCharge(new Entity<BatteryComponent?>(target, targetBat));
        var postCharge = _battery.GetCharge(cellEnt.Value.AsNullable());
        return postRoom <= 0 || postCharge <= 0;
    }

    private void StartInjectDoAfter(EntityUid user, EntityUid gloves, EntityUid target)
    {
        var delay = TimeSpan.FromSeconds(1.0);
        if (_powerCell.TryGetBatteryFromSlot(gloves, out var cellEnt) &&
            TryComp<BluespaceCapacitorBatteryComponent>(cellEnt.Value.Owner, out var bsCell))
            delay = TimeSpan.FromSeconds(bsCell.TransferCooldown);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay,
            new CapacitorInjectDoAfterEvent(), gloves, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 2f,
        });
    }

    private void OnInjectDoAfter(Entity<CapacitorGlovesComponent> gloves, ref CapacitorInjectDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            if (gloves.Comp.AutoInjectTarget != null)
            {
                gloves.Comp.AutoInjectTarget = null;
                if (gloves.Comp.WearerUid is { } w)
                    _popup.PopupEntity(Loc.GetString("capacitor-gloves-inject-out-of-range"), gloves.Owner, w);
            }
            return;
        }

        if (gloves.Comp.AutoInjectTarget is not { } target || gloves.Comp.WearerUid is not { } wearer)
            return;

        var done = DoSingleInjectTick(wearer, gloves.Owner, target, gloves.Comp);
        if (done)
        {
            gloves.Comp.AutoInjectTarget = null;
            gloves.Comp.InjectAvailableAt = _timing.CurTime + TimeSpan.FromSeconds(gloves.Comp.InjectCooldownTime);
            _popup.PopupEntity(Loc.GetString("capacitor-gloves-inject-done"), gloves.Owner, wearer);
            return;
        }

        // Repeat this do-after tick until injection is complete.
        args.Repeat = true;
    }

    private void OnWearerEmp(Entity<WornCapacitorGlovesComponent> worn, ref EmpPulseEvent args)
    {
        if (!_powerCell.TryGetBatteryFromSlot(worn.Comp.GlovesUid, out var cellEnt))
            return;

        // Bluespace cells are EMP-resistant.
        if (HasComp<BluespaceCapacitorBatteryComponent>(cellEnt.Value.Owner))
            return;

        var charge = _battery.GetCharge(cellEnt.Value.AsNullable());
        if (charge <= 0)
            return;

        _battery.TryUseCharge(cellEnt.Value.AsNullable(), charge);
        args.Affected = true;
    }
}

