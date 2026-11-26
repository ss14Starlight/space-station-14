using Content.Server._Starlight.NPC.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.NPC.Systems;

public sealed partial class NPCSpeechSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IViewVariablesManager _viewVariables = default!;

    private static readonly TimeSpan MinimumAttemptInterval = TimeSpan.FromSeconds(0.5);
    private readonly HashSet<string> _missingDialogueKeys = new(StringComparer.Ordinal);
    private TimeSpan _nextCheck;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // The default path writer dirties components, but NPC speech is server-only.
        var settings = _viewVariables.GetTypeHandler<NPCSpeechComponent>();
        AddSettingPath(settings, nameof(NPCSpeechComponent.Speech),
            component => component.Speech, (component, text) => component.Speech = text);
        AddSettingPath(settings, nameof(NPCSpeechComponent.MinimumInterval),
            component => component.MinimumInterval, (component, interval) => component.MinimumInterval = interval);
        AddSettingPath(settings, nameof(NPCSpeechComponent.MaximumInterval),
            component => component.MaximumInterval, (component, interval) => component.MaximumInterval = interval);
        AddSettingPath(settings, nameof(NPCSpeechComponent.SpeechChance),
            component => component.SpeechChance, (component, chance) => component.SpeechChance = chance);
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        var settings = _viewVariables.GetTypeHandler<NPCSpeechComponent>();
        settings.RemovePath(nameof(NPCSpeechComponent.Speech));
        settings.RemovePath(nameof(NPCSpeechComponent.MinimumInterval));
        settings.RemovePath(nameof(NPCSpeechComponent.MaximumInterval));
        settings.RemovePath(nameof(NPCSpeechComponent.SpeechChance));

        base.Shutdown();
    }

    private static void AddSettingPath<T>(ViewVariablesTypeHandler<NPCSpeechComponent> settings,
        string path, Func<NPCSpeechComponent, T> get, Action<NPCSpeechComponent, T> set)
    {
        settings.AddPath(path, component => ViewVariablesPath.FromGetter(() => get(component), typeof(T))
            .WithSetter(value =>
            {
                if (value is T setting)
                    set(component, setting);
            }));
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + MinimumAttemptInterval;

        var query = EntityQueryEnumerator<NPCSpeechComponent>();
        while (query.MoveNext(out var uid, out var speech))
        {
            var literal = speech.LiteralSpeech;
            var lines = literal ?? speech.SpeechPool;
            if (lines.Count == 0 || !(speech.SpeechChance > 0f))
            {
                speech.LastAttemptTime = null;
                speech.AttemptInterval = null;
                continue;
            }

            if (!TryAdvanceTimer(speech, now) || HasComp<ActorComponent>(uid) || _mobState.IsIncapacitated(uid) ||
                !_random.Prob(Math.Min(speech.SpeechChance, 1f)))
                continue;

            var entry = _random.Pick(lines);
            if (entry == null)
                continue;

            var line = literal == null ? NPCSpeechComponent.ResolveLine(_loc, entry) : entry;
            if (line == null)
            {
                if (_missingDialogueKeys.Add(entry.Trim()))
                    Log.Warning($"Missing NPCSpeech localization for '{entry}' on {ToPrettyString(uid)}.");

                continue;
            }

            line = line.Trim();
            if (line.Length == 0)
                continue;

            var chatType = InGameICChatType.Speak;
            if (line.StartsWith(SharedChatSystem.EmotesAltPrefix))
            {
                if (line.Length == 1)
                    continue;

                line = line[1..];
                chatType = InGameICChatType.Emote;
            }

            _chat.TrySendInGameICMessage(uid, line, chatType, hideChat: false, checkRadioPrefix: false);
        }
    }

    private bool TryAdvanceTimer(NPCSpeechComponent speech, TimeSpan now)
    {
        var interval = speech.AttemptInterval ??= SampleInterval(speech);
        if (speech.LastAttemptTime is not { } previous)
        {
            speech.LastAttemptTime = now;
            return false;
        }

        if (now - previous < interval)
            return false;

        // Advance before attempting speech so failed rolls and blocked speech also wait.
        speech.LastAttemptTime = now;
        speech.AttemptInterval = SampleInterval(speech);
        return true;
    }

    private TimeSpan SampleInterval(NPCSpeechComponent speech)
    {
        var minimum = speech.MinimumInterval < MinimumAttemptInterval ? MinimumAttemptInterval : speech.MinimumInterval;
        var maximum = speech.MaximumInterval < minimum ? minimum : speech.MaximumInterval;
        return _random.Next(minimum, maximum);
    }
}
