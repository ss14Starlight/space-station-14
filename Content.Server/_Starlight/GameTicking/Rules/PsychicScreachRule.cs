using Content.Server._FarHorizons.Silicons.Glitching;
using Content.Server._Starlight.Bluespace;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Body.Systems;
using Content.Server.GameTicking;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Stunnable;
using Content.Shared._FarHorizons.Silicons.Glitching;
using Content.Shared.Administration.Components;
using Content.Shared.Body.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Station.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events;

public sealed class PsychicScreachRule : StationEventSystem<PsychicScreachRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly VomitSystem _vomitSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
    [Dependency] private readonly GlitchingSystem _glitching = default!; // Far Horizons

    protected override void Started(EntityUid uid, PsychicScreachRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        comp.chosenStation = stationEvent.TargetStation;
        if (comp.chosenStation is null)
            if (!TryGetRandomStation(out comp.chosenStation))
                return;

        var allPlayersOnStation = Filter.Empty().AddWhere(session =>
            {
                if (session.AttachedEntity is null) return false;
                if (!TryComp<StationMemberComponent>(Transform(session.AttachedEntity.Value).GridUid,
                        out var stationGrid)) return false;
                return stationGrid.Station == stationEvent.TargetStation;
            });

        Audio.PlayGlobal(comp.Scream, allPlayersOnStation, true);
        Audio.PlayGlobal(comp.Atmosphere1, allPlayersOnStation, true);

        // Comms are disabled directly by SolarFlareRule.

        // Break Light at 10% And flicker them all!
        var lightquery = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lightquery.MoveNext(out var ent, out var light, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != comp.chosenStation)
                continue;

            if (_random.Prob(0.1f))
                _light.TryDestroyBulb(ent, light);
            else
            {
                _light.ToggleBlinkingLight(ent, light, true);
                Timer.Spawn(TimeSpan.FromSeconds(10), () => _light.ToggleBlinkingLight(ent, light, false));
            }
        }

        // Affect the crew
        var mobquery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobquery.MoveNext(out var ent, out var mob, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != comp.chosenStation)
                continue;

            if (mob.CurrentState == MobState.Dead)
                continue;

            // Nullspace is Shunt for every mob.
            var ev = new NullSpaceShuntEvent();
            RaiseLocalEvent(ent, ref ev);

            if (HasComp<BorgChassisComponent>(ent) || HasComp<GlitchOnEMPComponent>(ent) || HasComp<GlitchOnIonStormComponent>(ent))
            {
                _popup.PopupEntity(Loc.GetString("station-event-psychicscreach-borg"), ent, ent, PopupType.LargeCaution);
                _statusEffect.TryAddStatusEffectDuration(ent, "StatusEffectTemporaryBlindness", TimeSpan.FromSeconds(5));
                _glitching.ApplyGlitch(ent, TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(5));
                _stunSystem.TryAddStunDuration(ent, TimeSpan.FromSeconds(5));
            }
            else if (HasComp<BloodstreamComponent>(ent))
            {
                _popup.PopupEntity(Loc.GetString("station-event-psychicscreach-nosebleed"), ent, ent, PopupType.LargeCaution);
                _bloodstreamSystem.TryModifyBleedAmount(ent, 1f);
                _statusEffect.TryAddStatusEffectDuration(ent, "StatusEffectSeeingRainbow", TimeSpan.FromSeconds(30));
                _vomitSystem.Vomit(ent);
                _stunSystem.TryKnockdown(ent, TimeSpan.FromSeconds(1));
            }
        }

        // Trigger IonLaws // ! (MAKE SURE ITS SILENT!)
        _gameTicker.StartGameRule("IonStorm");

        Timer.Spawn(TimeSpan.FromSeconds(10), () => {
            Audio.PlayGlobal(comp.Atmosphere2, allPlayersOnStation, true);

            // Power Outage!
            var stationpowerquery = EntityQueryEnumerator<StationInfiniteBatteryTargetComponent, TransformComponent>();
            while (stationpowerquery.MoveNext(out var ent, out var _, out var xform))
            {
                if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != comp.chosenStation)
                    continue;

                var battery = EnsureComp<BatteryComponent>(ent);

                var todrain = battery.LastCharge;

                if (HasComp<BatteryInterfaceComponent>(ent)) // Only SMES/SubStation has BatteryInterface.
                    todrain /= 3;
                else
                    todrain = 0;


                _batterySystem.SetCharge((ent, battery), todrain);
            }
        });
    }

    protected override void Ended(EntityUid uid, PsychicScreachRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        // Yeah this is there just to avoid double announcement.
    }
}
