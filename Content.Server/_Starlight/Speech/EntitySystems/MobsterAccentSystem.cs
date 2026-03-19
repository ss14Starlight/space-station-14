using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class MobsterAccentSystem : EntitySystem
{
    [GeneratedRegex(@"(?<=\w\w)(in)g(?!\w)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexIng();

    [GeneratedRegex(@"(?<=\w)o[Rr](?=\w)")]
    private static partial Regex RegexLowerOr();

    [GeneratedRegex(@"(?<=\w)O[Rr](?=\w)")]
    private static partial Regex RegexUpperOr();

    [GeneratedRegex(@"(?<=\w)a[Rr](?=\w)")]
    private static partial Regex RegexLowerAr();

    [GeneratedRegex(@"(?<=\w)A[Rr](?=\w)")]
    private static partial Regex RegexUpperAr();

    [GeneratedRegex(@"^(\S+)")]
    private static partial Regex RegexFirstWord();

    [GeneratedRegex(@"(\S+)$")]
    private static partial Regex RegexLastWord();

    [GeneratedRegex(@"([.!?]+$)(?!.*[.!?])|(?<![.!?])$")]
    private static partial Regex RegexLastPunctuation();

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobsterAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public SpeechMessage Accentuate(SpeechMessage message, MobsterAccentComponent component)
    {
        message = _replacement.ApplyReplacements(message, "mobster");

        // thinking -> thinkin'
        message.Text = RegexIng().Replace(message.Text, "$1'");

        // or -> uh and ar -> ah
        message.Text = RegexLowerOr().Replace(message.Text, "uh");
        message.Tts = RegexLowerOr().Replace(message.Tts ?? message.Text, "uh");

        message.Text = RegexUpperOr().Replace(message.Text, "UH");
        message.Tts = RegexUpperOr().Replace(message.Tts ?? message.Text, "UH");

        message.Text = RegexLowerAr().Replace(message.Text, "ah");
        message.Tts = RegexLowerAr().Replace(message.Tts ?? message.Text, "ah");

        message.Text = RegexUpperAr().Replace(message.Text, "AH");
        message.Tts = RegexUpperAr().Replace(message.Tts ?? message.Text, "AH");

        // Prefix
        if (_random.Prob(0.15f))
        {
            var firstWordAllCaps = !RegexFirstWord().Match(message.Text).Value.Any(char.IsLower);
            var pick = _random.Next(1, 2);
            var prefix = Loc.GetString($"accent-mobster-prefix-{pick}");

            if (!firstWordAllCaps)
            {
                message.Text = message.Text[0].ToString().ToLower() + message.Text.Remove(0, 1);
                message.Tts = (message.Tts ?? message.Text)[0].ToString().ToLower() + (message.Tts ?? message.Text).Remove(0, 1);
            }
            else
            {
                prefix = prefix.ToUpper();
            }

            message.Text = prefix + " " + message.Text;
            message.Tts = prefix + " " + (message.Tts ?? message.Text);
        }

        message.Text = message.Text[0].ToString().ToUpper() + message.Text.Remove(0, 1);
        message.Tts = (message.Tts ?? message.Text)[0].ToString().ToUpper() + (message.Tts ?? message.Text).Remove(0, 1);

        // Suffixes
        if (_random.Prob(0.4f))
        {
            var lastWordAllCaps = !RegexLastWord().Match(message.Text).Value.Any(char.IsLower);
            var suffix = component.IsBoss
                ? Loc.GetString($"accent-mobster-suffix-boss-{_random.Next(1, 4)}")
                : Loc.GetString($"accent-mobster-suffix-minion-{_random.Next(1, 3)}");

            if (lastWordAllCaps)
                suffix = suffix.ToUpper();

            message.Text = RegexLastPunctuation().Replace(message.Text, suffix);
            message.Tts = RegexLastPunctuation().Replace(message.Tts ?? message.Text, suffix);
        }

        return message;
    }

    private void OnAccentGet(EntityUid uid, MobsterAccentComponent component, AccentGetEvent args) 
        => args.Message = Accentuate(args.Message, component);
}
