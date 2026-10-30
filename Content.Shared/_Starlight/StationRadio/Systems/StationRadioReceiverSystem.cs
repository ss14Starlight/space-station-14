using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Examine;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedStationRadioReceiverSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [SubscribeLocalEvent]
    private void OnRadioToggle(Entity<StationRadioReceiverComponent> ent, ref ActivateInWorldEvent _)
    {
        if (!ent.Comp.ToggleOnActivate)
            return;

        ent.Comp.Active = !ent.Comp.Active;
        UpdateVisuals(ent);
        Dirty(ent);
    }

    private void UpdateVisuals(Entity<StationRadioReceiverComponent> ent)
    {
        if (TryComp(ent, out AppearanceComponent? appearance))
            _appearance.SetData(ent, ToggleableVisuals.Enabled, ent.Comp.Active, appearance); // this is a bit of a hack
    }

    /// <summary>
    /// When a station radio is initialized, check any active Radio server for if there is an
    /// active song playing. If there is, attempt to resume play.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnReceiverMapInit(Entity<StationRadioReceiverComponent> ent, ref MapInitEvent args)
    {
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// Resume play when the server power returns.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnServerPowerChanged(Entity<StationRadioServerComponent> ent, ref PowerChangedEvent args)
    {
    }

    /// <summary>
    /// Method for getting the current volume of the station radio.
    /// </summary>
    protected static float GetGain(StationRadioReceiverComponent comp, bool powered)
        => comp.Active && powered ? comp.Volume : 0f;

    /// <summary>
    /// Alt Click / Context Menu Verb for turning down the volume of the radio.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetAltVerbs(Entity<StationRadioReceiverComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = !ent.Comp.BoostVolume ? "Increase Volume" : "Decrease Volume",
            Act = () =>
            {
                ent.Comp.BoostVolume = !ent.Comp.BoostVolume;
                Dirty(ent);
            }
        });
    }

    /// <summary>
    /// Stop broadcasting if the Radio Server loses power, despite the Vinyl Player and Rig still being powered.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnPowerChanged(Entity<StationRadioReceiverComponent> ent, ref PowerChangedEvent args)
    {
    }

    /// <summary>
    /// When the Radio Server is destroyed, stop all station radio receivers.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnServerTerminating(Entity<StationRadioServerComponent> ent, ref EntityTerminatingEvent args)
    {
    }

    /// <summary>
    /// When the Radio Rig is destroyed, stop all station radio receivers.
    /// </summary>
    [SubscribeLocalEvent]
    protected virtual void OnRigTerminating(Entity<RadioRigComponent> ent, ref EntityTerminatingEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnAnchorStateChanged(Entity<StationRadioServerComponent> ent, ref AnchorStateChangedEvent args)
    {
    }

    /// <summary>
    /// Display whether the station radio is at full or low volume when examined.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnExamined(Entity<StationRadioReceiverComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString(!ent.Comp.BoostVolume
            ? "station-radio-receiver-examine-low-volume"
            : "station-radio-receiver-examine-full-volume"));

}
