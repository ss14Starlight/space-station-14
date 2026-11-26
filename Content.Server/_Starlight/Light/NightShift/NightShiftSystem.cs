using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.NodeGroups;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Light.NightLighting;
using Content.Shared._Starlight.Time;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Light.NightShift;

public sealed partial class NightShiftSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedTimeSystem _time = default!;
    [Dependency] private StationSystem _station = default!;

    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(2);
    private static readonly SoundSpecifier _announcementSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + _updateInterval;

        var scheduled = IsScheduledNight();
        foreach (var station in _station.GetStations())
        {
            UpdateStation(station, EnsureComp<StationNightShiftComponent>(station), scheduled);
        }

        var lowPowerEnabled = _cfg.GetCVar(StarlightCCVars.NightShiftLowPower);
        var query = EntityQueryEnumerator<ApcLightingComponent, ApcComponent, PowerNetworkBatteryComponent>();
        while (query.MoveNext(out var uid, out var lighting, out var apc, out var battery))
        {
            UpdateLowPower(lighting, apc, battery, now);
            UpdateApc((uid, lighting), lowPowerEnabled);
        }

        UpdateLights(quiet: true);
    }

    #region Station

    [SubscribeLocalEvent]
    private void OnStationPostInit(Entity<StationDataComponent> ent, ref StationPostInitEvent args)
        => EnsureComp<StationNightShiftComponent>(ent);

    /// <summary>
    /// Forces, or stops forcing, the night shift on a station regardless of station time.
    /// Each source is tracked separately, the shift stays forced while any of them is.
    /// </summary>
    public void SetForced(Entity<StationNightShiftComponent?> station, EntityUid source, bool forced)
    {
        if (!Resolve(station, ref station.Comp, false))
            return;

        if (forced ? station.Comp.ForcedBy.Add(source) : station.Comp.ForcedBy.Remove(source))
            UpdateStation(station, station.Comp, IsScheduledNight());
    }

    /// <summary>
    /// Overrides the night shift on a station, <see cref="ApcLightingMode.Auto"/> lifts the override.
    /// </summary>
    public void SetOverride(Entity<StationNightShiftComponent?> station, ApcLightingMode mode)
    {
        if (!Resolve(station, ref station.Comp, false))
            return;

        station.Comp.Override = mode;
        UpdateStation(station, station.Comp, IsScheduledNight());
    }

    public bool IsStationNight(Entity<StationNightShiftComponent?> station)
        => Resolve(station, ref station.Comp, false) && station.Comp.Night;

    private bool IsScheduledNight()
    {
        if (!_cfg.GetCVar(StarlightCCVars.NightShiftEnabled))
            return false;

        var hour = _time.GetStationTime().Time.TotalHours;
        var start = _cfg.GetCVar(StarlightCCVars.NightShiftStartHour);
        var end = _cfg.GetCVar(StarlightCCVars.NightShiftEndHour);

        // The usual case, a night that runs past midnight.
        if (start > end)
            return hour >= start || hour < end;

        return hour >= start && hour < end;
    }

    private void UpdateStation(EntityUid station, StationNightShiftComponent comp, bool scheduled)
    {
        var wasSuppressed = comp.Suppressed;
        var alertLevel = CompOrNull<AlertLevelComponent>(station)?.CurrentLevel;
        comp.Suppressed = !string.IsNullOrEmpty(alertLevel) && !comp.PermittedAlertLevels.Contains(alertLevel);

        var night = comp.Override switch
        {
            ApcLightingMode.Night => true,
            ApcLightingMode.Day => false,
            _ => !comp.Suppressed && (scheduled || comp.ForcedBy.Count > 0),
        };

        if (night == comp.Night)
            return;

        comp.Night = night;

        // Turning the lights back up for an emergency, and down again after it, gets its own announcement.
        var emergency = comp.Override == ApcLightingMode.Auto && (night ? wasSuppressed : comp.Suppressed);
        var message = (night, emergency) switch
        {
            (true, false) => "station-event-nightshift-start-announcement",
            (true, true) => "station-event-nightshift-enable",
            (false, false) => "station-event-nightshift-end-announcement",
            (false, true) => "station-event-nightshift-disable",
        };

        if (_cfg.GetCVar(StarlightCCVars.NightShiftAnnounce))
        {
            _chat.DispatchStationAnnouncement(station,
                Loc.GetString(message),
                Loc.GetString("nightshift-announcement-sender"),
                announcementSound: _announcementSound);
        }

        // Don't leave it to the next update, the announcement just promised the lights would change.
        var lowPowerEnabled = _cfg.GetCVar(StarlightCCVars.NightShiftLowPower);
        var query = EntityQueryEnumerator<ApcLightingComponent>();
        while (query.MoveNext(out var uid, out var lighting))
        {
            if (_station.GetOwningStation(uid) == station)
                UpdateApc((uid, lighting), lowPowerEnabled);
        }

        // Lights of APCs that didn't change are left as they are.
        UpdateLights(quiet: true);
    }

    #endregion

    #region APC

    private void UpdateLowPower(ApcLightingComponent lighting, ApcComponent apc, PowerNetworkBatteryComponent battery, TimeSpan now)
    {
        var state = battery.NetworkBattery;

        if (!apc.MainBreakerEnabled)
        {
            lighting.DrainingSince = null;
            lighting.LowPower = false;
            return;
        }

        // Some slack, so the supply catching up after a load change doesn't count as draining.
        var draining = state.CurrentSupply - state.CurrentReceiving > MathF.Max(1f, state.CurrentSupply * 0.05f);
        if (draining)
        {
            lighting.DrainingSince ??= now;
            if (now - lighting.DrainingSince >= lighting.LowPowerDelay)
                lighting.LowPower = true;

            return;
        }

        lighting.DrainingSince = null;

        // Dimming the lights cuts the load, which can be enough to stop draining. Wait for the battery to
        // fill back up before turning them up again, or the APC would flicker in and out of low power.
        var charge = state.Capacity > 0 ? state.CurrentStorage / state.Capacity : 0f;
        if (lighting.LowPower && charge >= lighting.LowPowerRecoverCharge)
            lighting.LowPower = false;
    }

    private void UpdateApc(Entity<ApcLightingComponent> ent, bool lowPowerEnabled)
    {
        var lighting = ent.Comp;
        var (night, reason) = lighting.Mode switch
        {
            ApcLightingMode.Night => (true, NightLightingReason.Manual),
            ApcLightingMode.Day => (false, NightLightingReason.None),
            _ when _station.GetOwningStation(ent) is { } station && IsStationNight(station) => (true, NightLightingReason.Station),
            _ when lowPowerEnabled && lighting.LowPower => (true, NightLightingReason.LowPower),
            _ => (false, NightLightingReason.None),
        };

        if (lighting.Night != night || lighting.Reason != reason)
        {
            lighting.Night = night;
            lighting.Reason = reason;
            Dirty(ent, lighting);
        }
    }

    private void UpdateLights(bool quiet)
    {
        var query = EntityQueryEnumerator<PoweredLightComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var light, out var receiver))
        {
            // A light that lost its APC goes back to day lighting.
            UpdateLight((uid, light), GetNightLighting(receiver.Provider?.Net as ApcNet), quiet);
        }
    }

    private void UpdateLights(EntityUid apc, bool quiet)
    {
        if (!TryComp<ApcPowerProviderComponent>(apc, out var apcProvider) || apcProvider.Net is not ApcNet net)
            return;

        var lighting = GetNightLighting(net);
        foreach (var provider in net.Providers)
        {
            foreach (var receiver in provider.LinkedReceivers)
            {
#pragma warning disable CS0618 // Sadly can't make it in another way, needs to store Entity instead of Component to avoid Obsolete warning.
                if (TryComp<PoweredLightComponent>(receiver.Owner, out var light))
                    UpdateLight((receiver.Owner, light), lighting, quiet);
#pragma warning restore CS0618
            }
        }
    }

    private ApcLightingComponent? GetNightLighting(ApcNet? net)
    {
        if (net == null)
            return null;

        foreach (var apc in net.Apcs)
        {
#pragma warning disable CS0618 // Sadly can't make it in another way, needs to store Entity instead of Component to avoid Obsolete warning.
            if (TryComp<ApcLightingComponent>(apc.Owner, out var lighting) && lighting.Night)
                return lighting;
#pragma warning restore CS0618
        }

        return null;
    }

    private void UpdateLight(Entity<PoweredLightComponent> light, ApcLightingComponent? lighting, bool quiet)
    {
        if (lighting == null)
        {
            if (!RemComp<NightLightingDimmedComponent>(light))
                return;
        }
        else
        {
            var dimmed = EnsureComp<NightLightingDimmedComponent>(light);
            if (MathHelper.CloseTo(dimmed.EnergyMultiplier, lighting.NightEnergyMultiplier)
                && MathHelper.CloseTo(dimmed.RadiusMultiplier, lighting.NightRadiusMultiplier)
                && MathHelper.CloseTo(dimmed.PowerMultiplier, lighting.NightPowerMultiplier)
                && dimmed.Tint == lighting.NightTint)
            {
                return;
            }

            dimmed.EnergyMultiplier = lighting.NightEnergyMultiplier;
            dimmed.RadiusMultiplier = lighting.NightRadiusMultiplier;
            dimmed.PowerMultiplier = lighting.NightPowerMultiplier;
            dimmed.Tint = lighting.NightTint;
            Dirty(light, dimmed);
        }

        // A whole station switching at once shouldn't turn into a wall of tube clicks.
        if (quiet)
        {
            light.Comp.LastThunk = _timing.CurTime;
            Dirty(light);
        }

        _poweredLight.UpdateLight(light, light.Comp);
    }

    [SubscribeLocalEvent]
    private void OnSetLightingMode(EntityUid uid, ApcLightingComponent comp, ApcSetLightingModeMessage args)
    {
        if (comp.Mode == args.Mode || !Enum.IsDefined(args.Mode))
            return;

        if (!_accessReader.IsAllowed(args.Actor, uid))
        {
            _popup.PopupCursor(Loc.GetString("apc-component-insufficient-access"), args.Actor, PopupType.Medium);
            Dirty(uid, comp);
            return;
        }

        var now = _timing.CurTime;
        if (now < comp.NextToggle)
        {
            _popup.PopupCursor(Loc.GetString("apc-lighting-cooldown"), args.Actor, PopupType.Medium);
            Dirty(uid, comp);
            return;
        }

        comp.Mode = args.Mode;
        comp.NextToggle = now + comp.ToggleCooldown;
        Dirty(uid, comp);

        if (TryComp<ApcComponent>(uid, out var apc))
            _audio.PlayPvs(apc.OnReceiveMessageSound, uid, AudioParams.Default.WithVolume(-2f));

        _adminLogger.Add(LogType.ItemConfigure, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} set the lighting mode of {ToPrettyString(uid):entity} to {args.Mode}.");

        UpdateApc((uid, comp), _cfg.GetCVar(StarlightCCVars.NightShiftLowPower));
        UpdateLights(uid, quiet: false);
    }

    #endregion
}
