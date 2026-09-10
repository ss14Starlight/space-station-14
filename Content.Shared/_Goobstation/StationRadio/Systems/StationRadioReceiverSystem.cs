using Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.Construction.Components; // Starlight - _Goob -> _Goobstation
using Content.Shared.Interaction;
using Content.Shared.Power;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine; // Starlight - Shift Click to view what volume the radio is at.
using Content.Shared.Verbs; // Starlight - Alt click to lower volume.


// Starlight - _Goob -> _Goobstation
namespace Content.Shared._Goobstation.StationRadio.Systems;

public abstract partial class SharedStationRadioReceiverSystem : EntitySystem // Starlight - made abstract
{
    [Dependency] private SharedAudioSystem _audio = default!;
    // Starlight - Add Station Radio Resume Play
    // Starlight - End
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<StationRadioReceiverComponent, MapInitEvent>(OnReceiverMapInit); // Starlight - Add Radio Resume Play

        SubscribeLocalEvent<StationRadioServerComponent, PowerChangedEvent>(OnServerPowerChanged); // Starlight - Fix Server Broadcasting Music with no power.
        SubscribeLocalEvent<StationRadioServerComponent, EntityTerminatingEvent>(OnServerTerminating); // Starlight - When Server is destroyed, it should stop broadcasting.
        SubscribeLocalEvent<RadioRigComponent, EntityTerminatingEvent>(OnRigTerminating); // Starlight - When Rig is destroyed, it should stop broadcasting.

        SubscribeLocalEvent<StationRadioReceiverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs); // Starlight - Alt click to lower volume.
        SubscribeLocalEvent<StationRadioReceiverComponent, ExaminedEvent>(OnExamined); // Starlight - Shift Click to view what volume the radio is at.
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
    }

    protected virtual void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    protected virtual void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    /// <summary>
    /// When a station radio is initialized, check any active Radio server for if there is an
    /// active song playing. If there is, attempt to resume play.
    /// </summary>
    protected virtual void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// Resume play when the server power returns.
    /// </summary>
    protected virtual void OnServerPowerChanged(EntityUid uid, StationRadioServerComponent comp, PowerChangedEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    // Starlight - Start
    #region Starlight
    /// <summary>
    /// Method for getting the current volume of the station radio.
    /// </summary>
    protected static float GetGain(StationRadioReceiverComponent comp, bool powered)
        => comp.Active && powered ? 1f : 0f;

    /// <summary>
    /// Alt Click / Context Menu Verb for turning down the volume of the radio.
    /// </summary>
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
    protected virtual void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        // Starlight - Moved to Content.Client/_Starlight/StationRadio/Systems
    }

    /// <summary>
    /// When the Radio Server is destroyed, stop all station radio receivers.
    /// </summary>
    protected virtual void OnServerTerminating(EntityUid uid, StationRadioServerComponent comp, ref EntityTerminatingEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    /// <summary>
    /// When the Radio Rig is destroyed, stop all station radio receivers.
    /// </summary>
    protected virtual void OnRigTerminating(EntityUid uid, RadioRigComponent comp, ref EntityTerminatingEvent args)
    {
        // Starlight - Moved to Content.Server/_Starlight/StationRadio/Systems
    }

    [SubscribeLocalEvent]
    protected virtual void OnAttemptAnchor(EntityUid uid, StationRadioServerComponent comp, ref AnchorStateChangedEvent args)
    {
    }

    /// <summary>
    /// Display whether the station radio is at full or low volume when examined.
    /// </summary>
    private void OnExamined(EntityUid uid, StationRadioReceiverComponent comp, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(!comp.BoostVolume
            ? "station-radio-receiver-examine-low-volume"
            : "station-radio-receiver-examine-full-volume"));



    #endregion
    // Starlight - End
}
