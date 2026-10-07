using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Examine;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedStationRadioReceiverSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
    }

    [SubscribeLocalEvent]
    protected virtual void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
    }

    /// <summary>
    /// When a station radio is initialized, check any active Radio server for if there is an
    /// active song playing. If there is, attempt to resume play.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// Resume play when the server power returns.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
    }

    /// <summary>
    /// Method for getting the current volume of the station radio.
    /// </summary>
    protected static float GetGain(StationRadioReceiverComponent comp, bool powered)
        => comp.Active && powered ? 1f : 0f;

    /// <summary>
    /// Alt Click / Context Menu Verb for turning down the volume of the radio.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetAltVerbs(EntityUid uid, StationRadioReceiverComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = !comp.BoostVolume ? "Increase Volume" : "Decrease Volume",
            Act = () =>
            {
                comp.BoostVolume = !comp.BoostVolume;
                Dirty(uid, comp);
            }
        });
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
    }

    /// <summary>
    /// When the Radio Server is destroyed, stop all station radio receivers.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnServerTerminating(EntityUid uid, StationRadioServerComponent comp, ref EntityTerminatingEvent args)
    {
    }

    /// <summary>
    /// When the Radio Rig is destroyed, stop all station radio receivers.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnRigTerminating(EntityUid uid, RadioRigComponent comp, ref EntityTerminatingEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnAttemptAnchor(EntityUid uid, StationRadioServerComponent comp, ref AnchorStateChangedEvent args)
    {
    }

    /// <summary>
    /// Display whether the station radio is at full or low volume when examined.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnExamined(EntityUid uid, StationRadioReceiverComponent comp, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(!comp.BoostVolume
            ? "station-radio-receiver-examine-low-volume"
            : "station-radio-receiver-examine-full-volume"));

}
