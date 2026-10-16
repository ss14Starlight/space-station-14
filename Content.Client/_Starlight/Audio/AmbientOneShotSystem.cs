using Content.Client.Gameplay;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sound;
using Content.Shared.Random.Rules;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Audio;

public sealed partial class AmbientOneShotSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private RulesSystem _rules = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SoundCategorySystem _soundCategory = default!;
    [Dependency] private VacuumHearingSystem _vacuumHearing = default!;

    private const float HelmetVolume = -6f;

    private readonly Dictionary<string, TimeSpan> _next = new();
    private float _volumeSlider;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        Subs.CVar(_cfg, StarlightCCVars.EnvironmentVolume, v => _volumeSlider = SharedAudioSystem.GainToVolume(v), true);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AmbientOneShotPrototype>())
            _next.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted
            || _state.CurrentState is not GameplayState
            || _player.LocalEntity is not { } player
            || Transform(player).MapID == MapId.Nullspace)
            return;

        var now = _timing.RealTime;

        foreach (var proto in _proto.EnumeratePrototypes<AmbientOneShotPrototype>())
        {
            if (!_next.TryGetValue(proto.ID, out var next))
            {
                _next[proto.ID] = now + NextDelay(proto);
                continue;
            }

            if (now < next)
                continue;

            _next[proto.ID] = now + NextDelay(proto);

            if (_rules.IsTrue(player, _proto.Index(proto.Rules)))
                Play(proto, player);
        }
    }

    private TimeSpan NextDelay(AmbientOneShotPrototype proto)
        => TimeSpan.FromSeconds(_random.NextFloat(proto.Interval.X, proto.Interval.Y));

    private void Play(AmbientOneShotPrototype proto, EntityUid player)
    {
        var offset = _random.NextAngle().ToVec() * _random.NextFloat(proto.Distance.X, proto.Distance.Y);
        var coords = _xform.GetMoverCoordinates(player).Offset(offset);
        var volume = proto.Sound.Params.Volume + _volumeSlider;

        if (_vacuumHearing.HelmetOcclusionValue > 0f)
            volume += HelmetVolume;

        var audioParams = proto.Sound.Params.WithVolume(volume);

        using (_soundCategory.Exempt())
            _audio.PlayStatic(proto.Sound, Filter.Local(), coords, false, audioParams);
    }
}
