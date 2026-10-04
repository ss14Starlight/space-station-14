using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sound;
using Content.Shared._Starlight.Zones;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Audio;

public sealed partial class RoomReverbSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;

    private const float MinPressure = 30f;

    private static readonly TimeSpan DoorwayHold = TimeSpan.FromSeconds(1);

    private TimeSpan _lastInRoom;

    private float _volume = 1f;

    private EntityUid? _auxiliary;
    private EntityUid? _effect;
    private (string Preset, float Amount)? _current;

    public (string Preset, float Amount)? Forced;

    public (string Preset, float Amount)? Current => _current;
    public int AttachedSources { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        Subs.CVar(_cfg, StarlightCCVars.ReverbVolume, value => _volume = value, true);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => Clear());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var wanted = Forced ?? GetWanted();

        if (wanted != null)
            _lastInRoom = _timing.RealTime;
        else if (_current != null && IsBetweenRooms() && _timing.RealTime < _lastInRoom + DoorwayHold)
            wanted = _current;

        if (wanted == null)
        {
            Clear();
            return;
        }

        if (wanted != _current || !Exists(_auxiliary) || !Exists(_effect))
            Rebuild(wanted.Value.Preset, wanted.Value.Amount);

        var attached = 0;
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (audio.Global)
                continue;

            if (audio.Auxiliary != _auxiliary)
                _audio.SetAuxiliary(uid, audio, _auxiliary);

            attached++;
        }

        AttachedSources = attached;
    }

    private bool IsBetweenRooms()
    {
        if (!TryComp(_player.LocalEntity, out ZoneTrackerComponent? tracker) ||
            tracker.RoomSize > 0 ||
            Transform(_player.LocalEntity.Value).GridUid == null)
            return false;

        return !TryComp(_player.LocalEntity, out HearingPressureComponent? hearing) || hearing.Pressure >= MinPressure;
    }

    public (string Preset, float Amount)? GetWanted()
    {
        if (_volume <= 0f ||
            !TryComp(_player.LocalEntity, out ZoneTrackerComponent? tracker) ||
            tracker.RoomSize <= 0)
            return null;

        if (TryComp(_player.LocalEntity, out HearingPressureComponent? hearing) && hearing.Pressure < MinPressure)
            return null;

        var reverbId = tracker.Zone is { } zone && _proto.TryIndex(zone, out var zoneProto) && zoneProto.Reverb is { } id
            ? id
            : RoomReverbPrototype.Default;

        if (!_proto.TryIndex(reverbId, out var reverb) || reverb.Tiers.Count == 0)
            return null;

        return (reverb.PresetFor(tracker.RoomSize), reverb.Amount * _volume);
    }

    private void Rebuild(string presetId, float amount)
    {
        if (!_proto.TryIndex<AudioPresetPrototype>(presetId, out var preset))
            return;

        var oldAuxiliary = _auxiliary;
        var oldEffect = _effect;

        var effect = _audio.CreateEffect();
        var auxiliary = _audio.CreateAuxiliary();
        _audio.SetEffectPreset(effect.Entity, effect.Component, Scale(preset, amount));
        _audio.SetEffect(auxiliary.Entity, auxiliary.Component, effect.Entity);

        _auxiliary = auxiliary.Entity;
        _effect = effect.Entity;
        _current = (presetId, amount);

        // Sounds still on the old slot move over in the same update, before it goes away.
        Detach(oldAuxiliary, _auxiliary);
        Delete(oldAuxiliary, oldEffect);
    }

    private void Clear()
    {
        if (_auxiliary == null && _effect == null)
            return;

        Detach(_auxiliary, null);
        Delete(_auxiliary, _effect);
        _auxiliary = null;
        _effect = null;
        _current = null;
        AttachedSources = 0;
    }

    private void Detach(EntityUid? from, EntityUid? to)
    {
        if (from == null)
            return;

        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (audio.Auxiliary == from)
                _audio.SetAuxiliary(uid, audio, to);
        }
    }

    private void Delete(EntityUid? auxiliary, EntityUid? effect)
    {
        if (Exists(auxiliary))
            Del(auxiliary.Value);

        if (Exists(effect))
            Del(effect.Value);
    }

    private static ReverbProperties Scale(AudioPresetPrototype preset, float amount) => new()
    {
        Density = preset.Density,
        Diffusion = preset.Diffusion,
        Gain = preset.Gain * amount,
        GainHF = preset.GainHF,
        GainLF = preset.GainLF,
        DecayTime = preset.DecayTime,
        DecayHFRatio = preset.DecayHFRatio,
        DecayLFRatio = preset.DecayLFRatio,
        ReflectionsGain = preset.ReflectionsGain,
        ReflectionsDelay = preset.ReflectionsDelay,
        ReflectionsPan = preset.ReflectionsPan,
        LateReverbGain = preset.LateReverbGain,
        LateReverbDelay = preset.LateReverbDelay,
        LateReverbPan = preset.LateReverbPan,
        EchoTime = preset.EchoTime,
        EchoDepth = preset.EchoDepth,
        ModulationTime = preset.ModulationTime,
        ModulationDepth = preset.ModulationDepth,
        AirAbsorptionGainHF = preset.AirAbsorptionGainHF,
        HFReference = preset.HFReference,
        LFReference = preset.LFReference,
        RoomRolloffFactor = preset.RoomRolloffFactor,
        DecayHFLimit = preset.DecayHFLimit,
    };
}
