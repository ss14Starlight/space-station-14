using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared._Goobstation.StationRadio.Components; // Starlight  - Examine the station radio server to see if microphone is active.
using Content.Shared.Examine; // Starlight  - Examine the station radio server to see if microphone is active.

namespace Content.Shared.Radio.EntitySystems;

public abstract partial class SharedRadioDeviceSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    #region Toggling
    public void ToggleRadioMicrophone(EntityUid uid, EntityUid user, bool quiet = false, RadioMicrophoneComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetMicrophoneEnabled(uid, user, !component.Enabled, quiet, component);
    }

    public virtual void SetMicrophoneEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioMicrophoneComponent? component = null) { }

    public void ToggleRadioSpeaker(EntityUid uid, EntityUid user, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetSpeakerEnabled(uid, user, !component.Enabled, quiet, component);
    }

    public void SetSpeakerEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Enabled = enabled;
        Dirty(uid, component);

        if (!quiet && user != null)
        {
            var state = Loc.GetString(component.Enabled ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state");
            var message = Loc.GetString("handheld-radio-component-on-use", ("radioState", state));
            _popup.PopupEntity(message, user.Value, user.Value);
        }

        _appearance.SetData(uid, RadioDeviceVisuals.Speaker, component.Enabled);
        if (component.Enabled)
            EnsureComp<ActiveRadioComponent>(uid).Channels.UnionWith(component.Channels);
        else
            RemCompDeferred<ActiveRadioComponent>(uid);
    }
    #endregion

    // Starlight - Start
    #region Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioServerComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Examining a Radio Station Server will now tell you if it is recording or not.
    /// </summary>
    private void OnExamined(EntityUid uid, StationRadioServerComponent comp, ref ExaminedEvent args)
    {
        if (!TryComp<RadioMicrophoneComponent>(uid, out var mic))
            return;

        args.PushMarkup(Loc.GetString(mic.Enabled
            ? "station-radio-server-examine-not-recording"
            : "station-radio-server-examine-recording"));
    }
    #endregion
    // Starlight - End
}

