using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Sound;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Audio;

public sealed partial class SoundCategorySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private const float MinGain = 0.0001f;

    private readonly Dictionary<SoundCategory, float> _gains = new();
    private readonly Dictionary<string, SoundCategory> _files = new();
    private readonly List<(string Prefix, SoundCategory Category)> _prefixes = new();
    private readonly List<EntityUid> _pending = new();
    private int _exemptDepth;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesBefore.Add(typeof(AudioSystem));

        SubscribeLocalEvent<AudioComponent, ComponentInit>(OnAudioInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);

        BindVolume(SoundCategory.Effects, StarlightCCVars.EffectsVolume);
        BindVolume(SoundCategory.Footsteps, StarlightCCVars.FootstepsVolume);
        BindVolume(SoundCategory.Handling, StarlightCCVars.HandlingVolume);
        BindVolume(SoundCategory.Combat, StarlightCCVars.CombatVolume);
        BindVolume(SoundCategory.Voice, StarlightCCVars.VoiceVolume);
        BindVolume(SoundCategory.Announcement, StarlightCCVars.AnnouncementVolume);

        RebuildLookup();
    }

    private void BindVolume(SoundCategory category, CVarDef<float> cvar)
        => Subs.CVar(_cfg, cvar, value => _gains[category] = value, true);

    /// <summary>
    /// Sounds started while the returned scope is alive keep their volume as is.
    /// </summary>
    public ExemptScope Exempt()
    {
        _exemptDepth++;
        return new ExemptScope(this);
    }

    public readonly struct ExemptScope(SoundCategorySystem system) : IDisposable
    {
        public void Dispose() => system._exemptDepth--;
    }

    private void OnAudioInit(Entity<AudioComponent> ent, ref ComponentInit args)
    {
        if (_exemptDepth == 0)
            _pending.Add(ent);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SoundCategoryPrototype>() || args.WasModified<SoundCollectionPrototype>())
            RebuildLookup();
    }

    private void RebuildLookup()
    {
        _files.Clear();
        _prefixes.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<SoundCategoryPrototype>())
        {
            foreach (var prefix in proto.Paths)
            {
                _prefixes.Add((prefix, proto.Category));
            }

            foreach (var collection in proto.Collections)
            {
                foreach (var file in _proto.Index(collection).PickFiles)
                {
                    _files[file.ToString()] = proto.Category;
                }
            }
        }

        _prefixes.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var uid in _pending)
        {
            if (!TryComp(uid, out AudioComponent? audio)
                || GetCategory(audio) is not { } category
                || !_gains.TryGetValue(category, out var gain)
                || gain.Equals(1f))
                continue;

            var change = SharedAudioSystem.GainToVolume(MathF.Max(gain, MinGain));
            _audio.SetVolume(uid, audio.Params.Volume + change, audio);
        }

        _pending.Clear();
    }

    private SoundCategory? GetCategory(AudioComponent audio)
    {
        var file = audio.FileName;

        if (!file.StartsWith("/Audio/"))
            return null;

        if (_files.TryGetValue(file, out var exact))
            return exact == SoundCategory.Ignore ? null : exact;

        foreach (var (prefix, category) in _prefixes)
        {
            if (file.StartsWith(prefix))
                return category == SoundCategory.Ignore ? null : category;
        }

        return audio.Global ? null : SoundCategory.Effects;
    }
}
