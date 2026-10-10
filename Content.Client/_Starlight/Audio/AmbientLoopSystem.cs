using System.Linq;
using Content.Client.Audio;
using Content.Client.Gameplay;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sound;
using Content.Shared.Random.Rules;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Audio;

public sealed partial class AmbientLoopSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private ContentAudioSystem _contentAudio = default!;
    [Dependency] private RulesSystem _rules = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SoundCategorySystem _soundCategory = default!;
    [Dependency] private VacuumHearingSystem _vacuumHearing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private List<AmbientLoopPrototype> _loops = new();
    private AmbientLoopPrototype? _current;
    private EntityUid? _stream;
    private readonly HashSet<EntityUid> _fadingOut = new();
    private TimeSpan _nextCheck;
    private float _volumeSlider;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        Subs.CVar(_cfg, StarlightCCVars.StationHumVolume, OnVolumeChanged, true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        RefreshLoops();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        // Streams sit in nullspace, so nothing else cleans them up; also kill loops that are still fading out.
        foreach (var stream in _fadingOut)
            DeleteStream(stream);

        _fadingOut.Clear();
        Stop(0f);
        _current = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Stop(0f);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AmbientLoopPrototype>())
            RefreshLoops();
    }

    private void RefreshLoops()
    {
        _loops = _proto.EnumeratePrototypes<AmbientLoopPrototype>()
            .OrderByDescending(p => p.Priority)
            .ToList();
        _current = null;
        Stop(0f);
    }

    private void OnVolumeChanged(float value)
    {
        _volumeSlider = SharedAudioSystem.GainToVolume(value);

        if (_current != null && TryComp(_stream, out AudioComponent? audio))
            _audio.SetVolume(_stream, _current.Sound.Params.Volume + _volumeSlider, audio);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || _timing.RealTime < _nextCheck)
            return;

        _nextCheck = _timing.RealTime + CheckInterval;

        var wanted = _state.CurrentState is GameplayState ? GetLoop() : null;

        if (wanted == _current && (wanted == null || Exists(_stream)))
            return;

        Stop(_current?.FadeTime ?? 0f);
        _current = wanted;

        if (wanted != null)
            Play(wanted);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var occlusion = _vacuumHearing.AmbienceOcclusionValue;
        SetOcclusion(_stream, occlusion);
        foreach (var stream in _fadingOut)
            SetOcclusion(stream, occlusion);
    }

    private void SetOcclusion(EntityUid? stream, float occlusion)
    {
        if (TryComp(stream, out AudioComponent? audio) && MathF.Abs(audio.Occlusion - occlusion) > 0.01f)
            audio.Occlusion = occlusion;
    }

    private AmbientLoopPrototype? GetLoop()
    {
        if (_player.LocalEntity is not { } player || Transform(player).MapID == MapId.Nullspace)
            return null;

        foreach (var loop in _loops)
        {
            if (_rules.IsTrue(player, _proto.Index(loop.Rules)))
                return loop;
        }

        return null;
    }

    private void Play(AmbientLoopPrototype loop)
    {
        var audioParams = loop.Sound.Params
            .WithLoop(true)
            .WithVolume(loop.Sound.Params.Volume + _volumeSlider);

        (EntityUid Entity, AudioComponent Component)? stream;
        using (_soundCategory.Exempt())
            stream = _audio.PlayGlobal(loop.Sound, Filter.Local(), false, audioParams);
        _stream = stream?.Entity;

        if (stream != null)
            _contentAudio.FadeIn(_stream, stream.Value.Component, loop.FadeTime);
    }

    private void Stop(float fadeTime)
    {
        _fadingOut.RemoveWhere(s => !Exists(s));

        if (_stream is { } stream && fadeTime > 0f)
        {
            _contentAudio.FadeOut(stream, duration: fadeTime);
            _fadingOut.Add(stream);
        }
        else
        {
            DeleteStream(_stream);
        }

        _stream = null;
    }

    /// <summary>
    /// <see cref="SharedAudioSystem.Stop"/> silently does nothing outside first-time prediction
    /// (e.g. while a game state is applied and the local player gets detached), which leaked the stream.
    /// </summary>
    private void DeleteStream(EntityUid? stream)
    {
        if (stream is { } uid && Exists(uid) && IsClientSide(uid))
            QueueDel(uid);
    }
}
