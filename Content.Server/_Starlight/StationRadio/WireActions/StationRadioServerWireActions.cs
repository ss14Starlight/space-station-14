using Content.Server._Starlight.StationRadio.Systems;
using Content.Server.Wires;
using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Wires;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.WireActions;

public abstract partial class StationRadioServerWireAction : ComponentWireAction<StationRadioServerComponent>
{
    /// <summary>
    /// How long a pulse lasts.
    /// </summary>
    [DataField]
    public TimeSpan PulseDuration = TimeSpan.FromSeconds(10);

    protected StationRadioServerSystem Server = default!;
    protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        Server = EntityManager.System<StationRadioServerSystem>();
        Timing = IoCManager.Resolve<IGameTiming>();
    }

    protected void Refresh(Wire wire, StationRadioServerComponent comp) => Server.Refresh((wire.Owner, comp));
}

public sealed partial class StationRadioMusicWireAction : StationRadioServerWireAction
{
    public override string Name { get; set; } = "wire-name-station-radio-music";
    public override Color Color { get; set; } = Color.Green;
    public override object StatusKey { get; } = StationRadioServerWireStatus.Music;

    public override StatusLightState? GetLightState(Wire wire, StationRadioServerComponent comp) =>
        comp.MusicMuted ? StatusLightState.Off : StatusLightState.On;

    public override bool Cut(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.MusicCut = true;
        Refresh(wire, comp);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.MusicCut = false;
        Refresh(wire, comp);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.MusicMutedUntil = Timing.CurTime + PulseDuration;
        Refresh(wire, comp);
    }
}

public sealed partial class StationRadioMicrophoneWireAction : StationRadioServerWireAction
{
    public override string Name { get; set; } = "wire-name-station-radio-microphone";
    public override Color Color { get; set; } = Color.Blue;
    public override object StatusKey { get; } = StationRadioServerWireStatus.Microphone;

    public override StatusLightState? GetLightState(Wire wire, StationRadioServerComponent comp) =>
        Server.IsMicrophoneMuted(comp) ? StatusLightState.Off : StatusLightState.On;

    public override bool Cut(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.MicrophoneCut = true;
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.MicrophoneCut = false;
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, StationRadioServerComponent comp)
        => comp.MicrophoneMutedUntil = Timing.CurTime + PulseDuration;
}

public sealed partial class StationRadioInterferenceWireAction : StationRadioServerWireAction
{
    public override string Name { get; set; } = "wire-name-station-radio-interference";
    public override Color Color { get; set; } = Color.Orange;
    public override object StatusKey { get; } = StationRadioServerWireStatus.Interference;

    public override StatusLightState? GetLightState(Wire wire, StationRadioServerComponent comp) =>
        comp.Interference > 0f ? StatusLightState.BlinkingFast : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.InterferenceCut = true;
        Refresh(wire, comp);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        comp.InterferenceCut = false;
        Refresh(wire, comp);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, StationRadioServerComponent comp)
    {
        var until = Timing.CurTime + PulseDuration;
        if (until > comp.InterferenceUntil)
            comp.InterferenceUntil = until;

        Refresh(wire, comp);
    }
}
