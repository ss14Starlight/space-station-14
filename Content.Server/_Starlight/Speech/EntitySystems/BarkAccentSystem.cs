using Content.Shared.StatusEffectNew;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;
using Content.Shared._Starlight.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class BarkAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly IReadOnlyList<string> _barks = new List<string>{
        " Woof!", " WOOF", " wof-wof"
    }.AsReadOnly();

    private static readonly IReadOnlyDictionary<string, string> _specialWords = new Dictionary<string, string>()
    {
        { "ah", "arf" },
        { "Ah", "Arf" },
        { "oh", "oof" },
        { "Oh", "Oof" },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<BarkAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<BarkAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    public SpeechMessage Accentuate(SpeechMessage message)
    {
        foreach (var (word, repl) in _specialWords)
        {
            message.Text = message.Text.Replace(word, repl);
            message.Tts = (message.Tts ?? message.Text).Replace(word, repl);
        }

        message.Text = message.Text.Replace("!", _random.Pick(_barks))
            .Replace("l", "r")
            .Replace("L", "R");

        message.Tts = (message.Tts ?? message.Text).Replace("!", " Woof!");

        return message;
    }

    private void OnAccent(Entity<BarkAccentComponent> entity, ref AccentGetEvent args)
        => args.Message = Accentuate(args.Message);

    private void OnAccentRelayed(Entity<BarkAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        => args.Args.Message = Accentuate(args.Args.Message);
}
