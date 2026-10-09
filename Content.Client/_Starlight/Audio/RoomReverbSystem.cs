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
using Robust.Shared.Audio.Sources;
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

    private const float MaxReflectionGain = 1f;

    private static readonly TimeSpan _doorwayHold = TimeSpan.FromSeconds(1);

    private const int SoftRadius = 4;
    private const float SeatSoftness = 0.04f;
    private const float TableSoftness = 0.02f;
    private const float MaxSoftness = 0.6f;
    private static readonly TimeSpan _softnessInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _lastInRoom;
    private TimeSpan _nextSoftness;
    private float _softness;
    private readonly Dictionary<int, bool> _softTiles = [];
    private readonly HashSet<Entity<StrapComponent>> _seats = [];
    private readonly HashSet<string> _dryFiles = [];
    private readonly HashSet<Entity<ClimbableComponent>> _tables = [];

    private float _volume = 1f;

    private EntityUid? _auxiliary;
    private EntityUid? _effect;

    private const int ChurnLimit = 200;
    private static readonly TimeSpan _churnInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextChurnReport;
    private int _applyCalls;
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
        RebuildDryFiles();
    }

    [SubscribeLocalEvent]
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

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.RealTime >= _nextSoftness)
        {
            _nextSoftness = _timing.RealTime + _softnessInterval;
            _softness = GetSoftness();
        }

        var wanted = Forced ?? GetWanted();

        if (wanted != null)
            _lastInRoom = _timing.RealTime;
        else if (_current != null && IsBetweenRooms() && _timing.RealTime < _lastInRoom + _doorwayHold)
            wanted = _current;

        ReportChurn();

        if (wanted == null)
        {
            Clear();
            return;
        }

        if (!EnsureSlot())
            return;

        if (wanted != _current)
            Apply(wanted.Value);

        if (!TryComp(_auxiliary, out AudioAuxiliaryComponent? slot))
            return;

        // Straight onto the OpenAL source, never through AudioComponent.Auxiliary: that field is networked, and
        // touching it on a server sound made the engine re-apply the sound's state, seeking it to client time.
        // This causes the audio to play with the beginning cut off and to stretch out, creating a "drilling" effect.
        var attached = 0;
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var audio))
        {
            if (audio.Global || audio.Auxiliary != null || _dryFiles.Contains(audio.FileName))
                continue;

            ((IAudioSource) audio).SetAuxiliary(slot.Auxiliary);
            attached++;
        }

        AttachedSources = attached;
    }

    private void ReportChurn()
    {
        if (_timing.RealTime < _nextChurnReport)
            return;

        if (_applyCalls > ChurnLimit)
            Log.Warning($"Room reverb churn: {_applyCalls} preset changes in the last {_churnInterval.TotalSeconds} s");

        _applyCalls = 0;
        _nextChurnReport = _timing.RealTime + _churnInterval;
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

    // One slot and effect while the listener is in rooms, a room change only rewrites the effect's parameters.
    // Recreating them meant moving every playing sound to a new slot.
    private bool EnsureSlot()
    {
        if (Exists(_auxiliary) && Exists(_effect))
            return true;

        Delete(_auxiliary, _effect);

        var effect = _audio.CreateEffect();
        var auxiliary = _audio.CreateAuxiliary();
        _effect = effect.Entity;
        _auxiliary = auxiliary.Entity;
        _current = null;
        return true;
    }

    private void Apply(ReverbSettings settings)
    {
        if (!_proto.TryIndex<AudioPresetPrototype>(settings.Preset, out var preset) ||
            !TryComp(_effect, out AudioEffectComponent? effect) ||
            !TryComp(_auxiliary, out AudioAuxiliaryComponent? auxiliary))
            return;

        _audio.SetEffectPreset(_effect.Value, effect, Scale(preset, settings));

        // EFX copies the effect into the slot on assignment, so assign again after changing it.
        _audio.SetEffect(_auxiliary.Value, auxiliary, _effect.Value);
        _current = settings;
        _applyCalls++;
    }

    private void Clear()
    {
        if (_current == null && _auxiliary == null && _effect == null)
            return;

        Detach();
        Delete(_auxiliary, _effect);
        _auxiliary = null;
        _effect = null;
        _current = null;
        AttachedSources = 0;
    }

    private void Detach()
    {
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var audio))
        {
            // Leave sounds that use a slot of their own through the component alone.
            if (audio.Auxiliary == null)
                ((IAudioSource) audio).SetAuxiliary(null);
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
        ReflectionsGain = MathF.Min(preset.ReflectionsGain, MaxReflectionGain),
        ReflectionsDelay = Math.Clamp(preset.ReflectionsDelay * settings.DelayScale, 0f, 0.3f),
        ReflectionsPan = preset.ReflectionsPan,
        LateReverbGain = MathF.Min(preset.LateReverbGain, MaxReflectionGain),
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
