using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class ScrambledAccentSystem : EntitySystem
{
    [GeneratedRegex(@"(?<=\ )i(?=[\ \.\?]|$)")]
    private static partial Regex RegexLoneI();

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ScrambledAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<ScrambledAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    public string Accentuate(string message)
    {
        var words = message.ToLower().Split();

        if (words.Length < 2)
        {
            var pick = _random.Next(1, 8);
            return Loc.GetString($"accent-scrambled-words-{pick}");
        }

        var scrambled = words.OrderBy(x => _random.Next()).ToArray();
        var msg = string.Join(" ", scrambled);

        msg = msg[0].ToString().ToUpper() + msg[1..];
        msg = RegexLoneI().Replace(msg, "I");

        return msg;
    }

    private void OnAccent(Entity<ScrambledAccentComponent> entity, ref AccentGetEvent args)
        => args.Message.Text = args.Message.Tts = Accentuate(args.Message.Text);

    private void OnAccentRelayed(Entity<ScrambledAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        => args.Args.Message.Text = args.Args.Message.Tts = Accentuate(args.Args.Message.Text);
}
