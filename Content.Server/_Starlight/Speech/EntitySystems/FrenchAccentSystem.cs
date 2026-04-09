using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class FrenchAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"th", RegexOptions.IgnoreCase)]
    private static partial Regex RegexTh();

    [GeneratedRegex(@"(?<!\w)h", RegexOptions.IgnoreCase)]
    private static partial Regex RegexStartH();

    [GeneratedRegex(@"(?<=\w\w)[!?;:](?!\w)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexSpacePunctuation();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrenchAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public SpeechMessage Accentuate(SpeechMessage message, FrenchAccentComponent component)
    {
        message = _replacement.ApplyReplacements(message, "french");

        // replaces h with ' at the start of words (visual only)
        message.Text = RegexStartH().Replace(message.Text, "'");

        // spaces out ! ? : and ;
        message.Text = RegexSpacePunctuation().Replace(message.Text, " $&");

        // replaces th with 'z or 's depending on the case
        message.Text = ApplyThReplacement(message.Text);
        return message;
    }

    private static string ApplyThReplacement(string msg)
    {
        foreach (Match match in RegexTh().Matches(msg))
        {
            var uppercase = msg.Substring(match.Index, 2).Contains("TH");
            var Z = uppercase ? "Z" : "z";
            var S = uppercase ? "S" : "s";
            var idxLetter = match.Index + 2;

            if (msg.Length <= idxLetter)
            {
                msg = string.Concat(msg.AsSpan(0, match.Index), "'", Z);
            }
            else
            {
                var c = "aeiouy".Contains(msg.Substring(idxLetter, 1), StringComparison.CurrentCultureIgnoreCase) ? Z : S;
                msg = string.Concat(msg.AsSpan(0, match.Index), "'", c, msg.AsSpan(idxLetter));
            }
        }
        return msg;
    }

    private void OnAccentGet(EntityUid uid, FrenchAccentComponent component, AccentGetEvent args)
        => args.Message = Accentuate(args.Message, component);
}
