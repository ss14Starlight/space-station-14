using System.Linq;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Systems;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.Systems;

public sealed partial class StationRadioReceiverSystem : SharedStationRadioReceiverSystem
{
    private const float StaticGain = 0.6f;

    private const float MaxWaver = 0.7f;

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.StationRadioVolume, OnVolumeCfgChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(StarlightCCVars.StationRadioVolume, OnVolumeCfgChanged);
    }

    protected override void OnReceiverMapInit(EntityUid uid, StationRadioReceiverComponent component, MapInitEvent args) =>
        UpdateGain((uid, component));

    [SubscribeLocalEvent]
    private void OnVolumeChanged(EntityUid uid, StationRadioReceiverComponent component, StationRadioVolumeChangedEvent args)
    {
        component.ClientVolume = args.Volume;
        UpdateGain((uid, component));
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(EntityUid uid, StationRadioReceiverComponent component, ComponentShutdown args)
    {
        foreach (var (stream, _) in component.Streams.Values)
            EndStream(stream);

        component.Streams.Clear();
        component.StaticStream = EndStream(component.StaticStream);
    }

    [SubscribeLocalEvent]
    private void AfterHandleReceiverState(Entity<StationRadioReceiverComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var playerSession = _playerManager.LocalSession;
        if (playerSession == null)
            return;

        var comp = ent.Comp;

        var restart = comp.BoostVolume != comp.BoostVolumePrev;
        comp.BoostVolumePrev = comp.BoostVolume;

        foreach (var (source, (stream, playing)) in comp.Streams.ToArray())
        {
            if (!restart && comp.Tracks.TryGetValue(source, out var track) && track.SamePlayback(playing))
                continue;

            EndStream(stream);
            comp.Streams.Remove(source);
        }

        var muteParam = (comp.BoostVolume ? comp.BoostedParams : comp.DefaultParams).WithVolume(0f);
        foreach (var (source, track) in comp.Tracks)
        {
            if (comp.Streams.ContainsKey(source))
                continue;

            if (_audio.PlayEntity(track.Sound, playerSession, ent, muteParam)?.Entity is not { } stream)
                continue;

            if (TryComp<AudioComponent>(stream, out var audio))
                _audio.SetPlaybackPosition((stream, audio), (float) (_timing.CurTime - track.StartTime).TotalSeconds);

            comp.Streams[source] = (stream, track);
        }

        if (restart || comp.Interference <= 0f)
            comp.StaticStream = EndStream(comp.StaticStream);

        if (comp.Interference > 0f && comp.StaticStream == null)
            comp.StaticStream = _audio.PlayEntity(comp.StaticSound, playerSession, ent, muteParam.WithLoop(true))?.Entity;

        UpdateGain(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Interference > 0f)
                UpdateGain((uid, comp));
        }
    }

    private void UpdateGain(Entity<StationRadioReceiverComponent> ent)
    {
        var comp = ent.Comp;
        comp.ClientVolume ??= _cfg.GetCVar(StarlightCCVars.StationRadioVolume);

        var param = comp.BoostVolume ? comp.BoostedParams : comp.DefaultParams;
        var gain = GetGain(comp, _power.IsPowered(ent.Owner)) * comp.ClientVolume.Value;
        var music = gain * InterferenceWaver(ent, comp.Interference);

        foreach (var (source, (stream, _)) in comp.Streams)
        {
            if (!TryComp<AudioComponent>(stream, out var audio))
                continue;

            var trackGain = comp.Tracks.TryGetValue(source, out var track) ? track.Gain : 0f;
            _audio.SetVolume(stream, param.Volume, audio);
            _audio.SetGain(stream, music * trackGain, audio);
        }

        if (TryComp<AudioComponent>(comp.StaticStream, out var hiss))
        {
            _audio.SetVolume(comp.StaticStream, param.Volume, hiss);
            _audio.SetGain(comp.StaticStream, gain * comp.Interference * StaticGain, hiss);
        }
    }

    /// <summary>
    /// Calculates a time-varying attenuation factor for interference.
    /// </summary>
    private float InterferenceWaver(EntityUid uid, float interference)
    {
        if (interference <= 0f)
            return 1f;

        var t = (float) _timing.RealTime.TotalSeconds;
        var phase = uid.Id * 1.618f;
        var fade = 0.5f + (0.5f * MathF.Sin((t * 5.3f) + phase) * MathF.Sin((t * 1.7f) + (phase * 0.5f)));
        return 1f - (MaxWaver * interference * fade);
    }

    private EntityUid? EndStream(EntityUid? uid)
    {
        if (TryComp<AudioComponent>(uid, out var audioComponent))
            audioComponent.StopPlaying();
        return _audio.Stop(uid, audioComponent);
    }

    private void OnVolumeCfgChanged(float volume)
    {
        var receiverQuery = EntityQueryEnumerator<StationRadioReceiverComponent>();
        foreach (var receiver in receiverQuery)
            RaiseLocalEvent(receiver, new  StationRadioVolumeChangedEvent(volume));
    }

    protected override void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args) =>
        UpdateGain((uid, comp));
}

[Serializable]
public sealed class StationRadioVolumeChangedEvent(float volume) : EntityEventArgs
{
    public float Volume = volume;
}
