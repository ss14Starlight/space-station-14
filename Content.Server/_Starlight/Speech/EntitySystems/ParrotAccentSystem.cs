using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class ParrotAccentSystem : EntitySystem
{
    [GeneratedRegex("[^A-Za-z0-9 -]")]
    private static partial Regex WordCleanupRegex();

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ParrotAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<ParrotAccentComponent> entity, ref AccentGetEvent args) 
        => args.Message.Text = args.Message.Tts = Accentuate(entity, args.Message.Text);

    public string Accentuate(Entity<ParrotAccentComponent> entity, string message)
    {
        if (_random.Prob(entity.Comp.LongestWordRepeatChance))
        {
            var cleaned = WordCleanupRegex().Replace(message, string.Empty);
            var words = cleaned.Split(null).Reverse();
            var longest = words.MaxBy(word => word.Length);
            if (longest?.Length >= entity.Comp.LongestWordMinLength)
            {
                message = EnsurePunctuation(message);
                longest = string.Concat(longest[0].ToString().ToUpper(), longest.AsSpan(1));
                message = string.Format("{0} {1} {2}!", message, GetRandomSquawk(entity), longest);
                return message;
            }
        }

        if (_random.Prob(entity.Comp.SquawkPrefixChance))
        {
            message = string.Format("{0} {1}", GetRandomSquawk(entity), message);
        }
        else
        {
            message = EnsurePunctuation(message);
            message = string.Format("{0} {1}", message, GetRandomSquawk(entity));
        }

        return message;
    }

    private static string EnsurePunctuation(string message)
    {
        if (!message.EndsWith('!') && !message.EndsWith('?') && !message.EndsWith('.'))
            return message + '!';
        return message;
    }

    private string GetRandomSquawk(Entity<ParrotAccentComponent> entity) 
        => Loc.GetString(_random.Pick(entity.Comp.Squawks));
}
