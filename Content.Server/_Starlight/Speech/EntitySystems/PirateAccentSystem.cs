using System.Linq;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;
using System.Text.RegularExpressions;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class PirateAccentSystem : EntitySystem
{
    [GeneratedRegex(@"^(\S+)")]
    private static partial Regex FirstWordAllCapsRegex();

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PirateAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public SpeechMessage Accentuate(SpeechMessage message, PirateAccentComponent component)
    {
        message = _replacement.ApplyReplacements(message, "pirate");

        if (!_random.Prob(component.YarrChance))
            return message;

        var firstWordAllCaps = !FirstWordAllCapsRegex().Match(message.Text).Value.Any(char.IsLower);

        var pick = _random.Pick(component.PirateWords);
        var pirateWord = Loc.GetString(pick);

        if (!firstWordAllCaps)
        {
            message.Text = message.Text[0].ToString().ToLower() + message.Text[1..];
            var tts = message.Tts ?? message.Text;
            if (tts.Length > 0)
                message.Tts = tts[0].ToString().ToLower() + tts[1..];
        }
        else
        {
            pirateWord = pirateWord.ToUpper();
        }

        message.Text = pirateWord + " " + message.Text;
        message.Tts = pirateWord + " " + (message.Tts ?? message.Text);

        return message;
    }

    private void OnAccentGet(EntityUid uid, PirateAccentComponent component, AccentGetEvent args) 
        => args.Message = Accentuate(args.Message, component);
}
