using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class GermanAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"(?<=\s|^)th", RegexOptions.IgnoreCase)]
    private static partial Regex RegexTh();

    [GeneratedRegex(@"(?<=\s|^)the(?=\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexThe();

    public override void Initialize()
        => SubscribeLocalEvent<GermanAccentComponent, AccentGetEvent>(OnAccent);

    public SpeechMessage Accentuate(SpeechMessage message)
    {
        // rarely, "the" should become "das" instead of "ze"
        foreach (Match match in RegexThe().Matches(message.Text))
        {
            if (_random.Prob(0.3f))
            {
                message.Text = message.Text[..match.Index] +
                      (char)(message.Text[match.Index] - 16) +
                      (char)(message.Text[match.Index + 1] - 7) +
                      (char)(message.Text[match.Index + 2] + 14) +
                      message.Text[(match.Index + 3)..];

                var tts = message.Tts ?? message.Text;
                if (tts.Length > match.Index + 2)
                    message.Tts = tts[..match.Index] +
                          (char)(tts[match.Index] - 16) +
                          (char)(tts[match.Index + 1] - 7) +
                          (char)(tts[match.Index + 2] + 14) +
                          tts[(match.Index + 3)..];
            }
        }

        // apply word replacements
        message = _replacement.ApplyReplacements(message, "german");

        // replace th with zh (visual only for msg, TTS can handle th)
        var msgBuilder = new StringBuilder(message.Text);
        foreach (Match match in RegexTh().Matches(message.Text))
        {
            msgBuilder[match.Index] = (char)(msgBuilder[match.Index] + 6);
        }

        // Random Umlaut Time! (visual only)
        var umlautCooldown = 0;
        for (var i = 0; i < msgBuilder.Length; i++)
        {
            if (umlautCooldown == 0)
            {
                if (_random.Prob(0.1f))
                {
                    msgBuilder[i] = msgBuilder[i] switch
                    {
                        'A' => 'Ä',
                        'a' => 'ä',
                        'O' => 'Ö',
                        'o' => 'ö',
                        'U' => 'Ü',
                        'u' => 'ü',
                        _ => msgBuilder[i]
                    };
                    umlautCooldown = 4;
                }
            }
            else
            {
                umlautCooldown--;
            }
        }

        message.Text = msgBuilder.ToString();
        return message;
    }

    private void OnAccent(Entity<GermanAccentComponent> ent, ref AccentGetEvent args)
        => args.Message = Accentuate(args.Message);
}
