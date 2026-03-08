using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class SkeletonAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"(?<!\w)[^aeiou]one", RegexOptions.IgnoreCase)]
    private static partial Regex BoneRegex();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkeletonAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public SpeechMessage Accentuate(SpeechMessage message, SkeletonAccentComponent component)
    {
        // bone replacements
        message.Text = BoneRegex().Replace(message.Text, "bone");
        message.Tts = BoneRegex().Replace(message.Tts ?? message.Text, "bone");

        // apply word replacements
        message = _replacement.ApplyReplacements(message, "skeleton");

        // Suffix
        if (_random.Prob(component.ackChance))
        {
            var suffix = " " + Loc.GetString("skeleton-suffix");
            message.Text += suffix;
            message.Tts = (message.Tts ?? message.Text) + suffix;
        }

        return message;
    }

    private void OnAccentGet(EntityUid uid, SkeletonAccentComponent component, AccentGetEvent args) 
        => args.Message = Accentuate(args.Message, component);
}
