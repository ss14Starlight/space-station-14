using Content.Server.Ninja.Events;
using Content.Server.Power.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles the doafter and power transfer when draining.
/// </summary>
public sealed class BatteryDrainerSystem : SharedBatteryDrainerSystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryDrainerComponent, ComponentStartup>(OnStartup); // imp add
        SubscribeLocalEvent<BatteryDrainerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<BatteryDrainerComponent, NinjaBatteryChangedEvent>(OnBatteryChanged);
    }

    /// <summary>
    ///  Imp add. Allow entities who are a battery to use themselves as the battery for this component
    /// </summary>
    private void OnStartup(Entity<BatteryDrainerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.BatteryUid == null && (TryComp<BatteryComponent>(ent.Owner, out _) || TryComp<PredictedBatteryComponent>(ent.Owner, out _))) // Starlight Edit: Check for PredictedBattery too.
            ent.Comp.BatteryUid = ent.Owner;
    }

    /// <summary>
    /// Start do after for draining a power source.
    /// Can't predict PNBC existing so only done on server.
    /// </summary>
    private void OnBeforeInteractHand(Entity<BatteryDrainerComponent> ent, ref BeforeInteractHandEvent args)
    {
        var (uid, comp) = ent;
        var target = args.Target;
        if (args.Handled || comp.BatteryUid is not { } battery || !HasComp<PowerNetworkBatteryComponent>(target))
            return;

        // handles even if battery is full so you can actually see the poup
        args.Handled = true;

        // Starlight edit Start: Check for PredictedBatteryComponent first, then BatteryComponent
        var isFull = TryComp<PredictedBatteryComponent>(battery, out _) 
            ? _predictedBattery.IsFull(battery) 
            : _battery.IsFull(battery);
        
        if (isFull)
        // Starlight edit End
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-full"), uid, uid, PopupType.Medium);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.DrainTime, new DrainDoAfterEvent(), target: target, eventTarget: uid)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnBatteryChanged(Entity<BatteryDrainerComponent> ent, ref NinjaBatteryChangedEvent args)
    {
        SetBattery((ent, ent.Comp), args.Battery);
    }

    /// <inheritdoc/>
    protected override void OnDoAfterAttempt(Entity<BatteryDrainerComponent> ent, ref DoAfterAttemptEvent<DrainDoAfterEvent> args)
    {
        base.OnDoAfterAttempt(ent, ref args);

        // Starlight edit Start
        if (ent.Comp.BatteryUid is not { } battery)
        {
            args.Cancel();
            return;
        }

        var isFull = TryComp<PredictedBatteryComponent>(battery, out _)
            ? _predictedBattery.IsFull(battery)
            : _battery.IsFull(battery);

        if (isFull)
        // Starlight edit End
            args.Cancel();
    }

    /// <inheritdoc/>
    protected override bool TryDrainPower(Entity<BatteryDrainerComponent> ent, EntityUid target)
    {
        var (uid, comp) = ent;
        if (comp.BatteryUid == null || !TryComp<BatteryComponent>(comp.BatteryUid.Value, out var battery))
            return false;

        if (!TryComp<BatteryComponent>(target, out var targetBattery) || !TryComp<PowerNetworkBatteryComponent>(target, out var pnb))
            return false;

        var available = _battery.GetCharge((target, targetBattery));
        if (MathHelper.CloseToPercent(available, 0))
        {
            _popup.PopupEntity(Loc.GetString("battery-drainer-empty", ("battery", target)), uid, uid, PopupType.Medium);
            return false;
        }

        var required = battery.MaxCharge - _battery.GetCharge((comp.BatteryUid.Value, battery));
        // higher tier storages can charge more
        // IMP EDIT START- why the fuck does draintime affecting the amount drained go undocumented!!!
        var maxDrained = comp.FullDrain ?
            pnb.MaxSupply * comp.DrainTime :
            required;
        // IMP EDIT END
        var input = Math.Min(Math.Min(available, required / comp.DrainEfficiency), maxDrained);
        if (!_battery.TryUseCharge((target, targetBattery), input))
            return false;

        var output = input * comp.DrainEfficiency;
        // PowerCells use PredictedBatteryComponent
        // SMES, substations and APCs use BatteryComponent
        _predictedBattery.ChangeCharge((comp.BatteryUid.Value, battery), output);
        // Starlight Start: Add minimum Clamp
        if (comp.MinimumDrain is not null)
            output = Math.Max(output, (float)comp.MinimumDrain);
        // Starlight End
        // TODO: create effect message or something
        Spawn("EffectSparks", Transform(target).Coordinates);
        _audio.PlayPvs(comp.SparkSound, target);
        _popup.PopupEntity(Loc.GetString("battery-drainer-success", ("battery", target)), uid, uid);

        // IMP ADD- god this code is a mess. the bool return is only ever used to check if this should repeat
        // we dont want that if we're draining the full thing so whatever
        if (comp.FullDrain)
            return false;
        // IMP ADD END

        // repeat the doafter until battery is full
        return !_battery.IsFull((comp.BatteryUid.Value, battery));
    }
}
