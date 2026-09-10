using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Systems;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Interaction;
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

    [SubscribeLocalEvent]
    private void OnVolumeChanged(EntityUid uid, StationRadioReceiverComponent component, StationRadioVolumeChangedEvent args)
    {
        component.ClientVolume = args.Volume;
        if (!TryComp<AudioComponent>(component.SoundEntity, out var audio))
            return;
        _audio.SetGain(component.SoundEntity, GetGain(component, _power.IsPowered(uid)) * (component.ClientVolume ?? 0), audio);
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(EntityUid uid, StationRadioReceiverComponent component, ComponentShutdown args)
    {
        if (component.SoundEntity == null)
            return;

        component.SoundEntity = _audio.Stop(component.SoundEntity);
    }

    [SubscribeLocalEvent]
    private void AfterHandleReceiverState(Entity<StationRadioReceiverComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var playerSession = _playerManager.LocalSession;
        if (playerSession == null)
            return;

        var (uid, comp) = ent;

        if (comp.CurrentSound == null)
        {
            comp.SoundEntity = EndStream(comp.SoundEntity);
            return;
        }

        if (comp.BoostVolume != comp.BoostVolumePrev)
            comp.SoundEntity = EndStream(comp.SoundEntity);



        var currentParam = comp.BoostVolume ? comp.BoostedParams : comp.DefaultParams;
        var muteParam = currentParam.WithVolume(0f);
        comp.SoundEntity ??= _audio.PlayEntity(comp.CurrentSound, playerSession, uid, muteParam)?.Entity;
        comp.BoostVolumePrev =  comp.BoostVolume;

        if (comp.SoundEntity == null || !TryComp<AudioComponent>(comp.SoundEntity, out var audio))
            return;

        var playOffset = _timing.CurTime - comp.StartTime;
        if (playOffset is not null)
            _audio.SetPlaybackPosition((comp.SoundEntity.Value, audio), (float)playOffset.Value.TotalSeconds);


        comp.ClientVolume ??= _cfg.GetCVar(StarlightCCVars.StationRadioVolume);
        _audio.SetVolume(comp.SoundEntity, currentParam.Volume, audio);
        _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)) * (comp.ClientVolume ?? 0), audio);
    }

    //TODO Replace all usages of this with _audio.Stop whenever upstream fixes it
    private EntityUid? EndStream(EntityUid? uid)
    {
        if (TryComp<AudioComponent>(uid, out var audioComponent))
            audioComponent.StopPlaying();  // <- I'm pretty sure robust is supposed to do this.
        return _audio.Stop(uid, audioComponent);
    }


    private void OnVolumeCfgChanged(float volume)
    {
        var receiverQuery = EntityQueryEnumerator<StationRadioReceiverComponent>();
        foreach (var receiver in receiverQuery)
        {
            RaiseLocalEvent(receiver, new  StationRadioVolumeChangedEvent(volume));
        }
    }

    protected override void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if (!TryComp<AudioComponent>(comp.SoundEntity, out var audio))
            return;
        _audio.SetGain(comp.SoundEntity, GetGain(comp, _power.IsPowered(uid)) * (comp.ClientVolume ?? 0), audio);
    }
}

[Serializable]
public sealed class StationRadioVolumeChangedEvent(float volume) : EntityEventArgs
{
    public float Volume = volume;
}
