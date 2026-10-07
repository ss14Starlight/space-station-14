using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sound;
using Content.Shared._Starlight.Zones;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Maps;
using Content.Shared.Speech;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;

    private const float MinPressure = 30f;

    private const float MinDiffusion = 0.9f;

    private static readonly TimeSpan DoorwayHold = TimeSpan.FromSeconds(1);

    private const int SoftRadius = 4;
    private const float SeatSoftness = 0.04f;
    private const float TableSoftness = 0.02f;
    private const float MaxSoftness = 0.6f;
    private static readonly TimeSpan SoftnessInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _lastInRoom;
    private TimeSpan _nextSoftness;
    private float _softness;
    private readonly Dictionary<int, bool> _softTiles = new();
    private readonly HashSet<Entity<StrapComponent>> _seats = new();
    private readonly HashSet<string> _dryFiles = new();
    private readonly HashSet<Entity<ClimbableComponent>> _tables = new();

    private float _volume = 1f;

    private EntityUid? _auxiliary;
    private EntityUid? _effect;
    private ReverbSettings? _current;

    public ReverbSettings? Forced;

    public ReverbSettings? Current => _current;
    public int AttachedSources { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        Subs.CVar(_cfg, StarlightCCVars.ReverbVolume, value => _volume = value, true);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => Clear());
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        RebuildDryFiles();
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SpeechSoundsPrototype>() || args.WasModified<SoundCollectionPrototype>())
            RebuildDryFiles();
    }

    private void RebuildDryFiles()
    {
        _dryFiles.Clear();

        foreach (var speech in _proto.EnumeratePrototypes<SpeechSoundsPrototype>())
        {
            AddDry(speech.SaySound);
            AddDry(speech.AskSound);
            AddDry(speech.ExclaimSound);
        }
    }

    private void AddDry(SoundSpecifier sound)
    {
        switch (sound)
        {
            case SoundPathSpecifier path:
                _dryFiles.Add(path.Path.ToString());
                break;
            case SoundCollectionSpecifier { Collection: { } collection }
                when _proto.TryIndex<SoundCollectionPrototype>(collection, out var proto):
                foreach (var file in proto.PickFiles)
                {
                    _dryFiles.Add(file.ToString());
                }
                break;
        }
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

        if (_timing.RealTime >= _nextSoftness)
        {
            _nextSoftness = _timing.RealTime + SoftnessInterval;
            _softness = GetSoftness();
        }

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
            Rebuild(wanted.Value);

        var attached = 0;
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (audio.Global || _dryFiles.Contains(audio.FileName))
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

    public ReverbSettings? GetWanted()
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

        var tier = reverb.TierFor(tracker.RoomSize);

        var damping = MathF.Round((1f - _softness) * 10f) / 10f;

        return new ReverbSettings(
            tier.Preset,
            reverb.Amount * _volume * damping,
            tier.DecayScale * (0.5f + (0.5f * damping)),
            tier.DelayScale);
    }

    private float GetSoftness()
    {
        if (_player.LocalEntity is not { } player ||
            Transform(player).GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
            return 0f;

        var center = _maps.TileIndicesFor(gridUid, grid, Transform(player).Coordinates);
        var soft = 0;
        var total = 0;

        for (var x = -SoftRadius; x <= SoftRadius; x++)
        {
            for (var y = -SoftRadius; y <= SoftRadius; y++)
            {
                if (!_maps.TryGetTileRef(gridUid, grid, center + new Vector2i(x, y), out var tile) || tile.Tile.IsEmpty)
                    continue;

                total++;
                if (IsSoftTile(tile.Tile.TypeId))
                    soft++;
            }
        }

        _seats.Clear();
        _lookup.GetEntitiesInRange(Transform(player).Coordinates, SoftRadius, _seats);
        _tables.Clear();
        _lookup.GetEntitiesInRange(Transform(player).Coordinates, SoftRadius, _tables);

        var softness = (total > 0 ? soft / (float) total : 0f)
            + (_seats.Count * SeatSoftness)
            + (_tables.Count * TableSoftness);
        return Math.Clamp(softness, 0f, MaxSoftness);
    }

    private bool IsSoftTile(int typeId)
    {
        if (_softTiles.TryGetValue(typeId, out var soft))
            return soft;

        soft = _tileDefs[typeId] is ContentTileDefinition { FootstepSounds: SoundCollectionSpecifier { Collection: { } collection } }
            && (collection == "FootstepCarpet" || collection == "FootstepGrass");
        _softTiles[typeId] = soft;
        return soft;
    }

    private void Rebuild(ReverbSettings settings)
    {
        if (!_proto.TryIndex<AudioPresetPrototype>(settings.Preset, out var preset))
            return;

        var oldAuxiliary = _auxiliary;
        var oldEffect = _effect;

        var effect = _audio.CreateEffect();
        var auxiliary = _audio.CreateAuxiliary();
        _audio.SetEffectPreset(effect.Entity, effect.Component, Scale(preset, settings));
        _audio.SetEffect(auxiliary.Entity, auxiliary.Component, effect.Entity);

        _auxiliary = auxiliary.Entity;
        _effect = effect.Entity;
        _current = settings;

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

    private static ReverbProperties Scale(AudioPresetPrototype preset, ReverbSettings settings) => new()
    {
        Density = preset.Density,
        Diffusion = MathF.Max(preset.Diffusion, MinDiffusion),
        Gain = preset.Gain * settings.Amount,
        GainHF = preset.GainHF,
        GainLF = preset.GainLF,
        DecayTime = Math.Clamp(preset.DecayTime * settings.DecayScale, 0.1f, 20f),
        DecayHFRatio = preset.DecayHFRatio,
        DecayLFRatio = preset.DecayLFRatio,
        ReflectionsGain = preset.ReflectionsGain,
        ReflectionsDelay = Math.Clamp(preset.ReflectionsDelay * settings.DelayScale, 0f, 0.3f),
        ReflectionsPan = preset.ReflectionsPan,
        LateReverbGain = preset.LateReverbGain,
        LateReverbDelay = Math.Clamp(preset.LateReverbDelay * settings.DelayScale, 0f, 0.1f),
        LateReverbPan = preset.LateReverbPan,
        EchoTime = preset.EchoTime,
        EchoDepth = 0f,
        ModulationTime = preset.ModulationTime,
        ModulationDepth = preset.ModulationDepth,
        AirAbsorptionGainHF = preset.AirAbsorptionGainHF,
        HFReference = preset.HFReference,
        LFReference = preset.LFReference,
        RoomRolloffFactor = preset.RoomRolloffFactor,
        DecayHFLimit = preset.DecayHFLimit,
    };
}

public record struct ReverbSettings(string Preset, float Amount, float DecayScale, float DelayScale);
