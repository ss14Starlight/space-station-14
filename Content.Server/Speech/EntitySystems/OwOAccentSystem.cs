using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;
using Robust.Shared.Reflection;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class OwOAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        private static readonly IReadOnlyDictionary<string, string> SpecialWords = new Dictionary<string, string>()
        {
            { "you", "wu" },
            { "are", "r" },
            { "hello", "mew" },
            { "love", "luv" },
            { "please", "plez" },
            { "food", "noms" },
            { "cute", "koot" },
            { "now", "meow" },
            { "look", "lookee" },
            { "little", "lil" },
        };

        public override void Initialize()
        {
            SubscribeLocalEvent<OwOAccentComponent, AccentGetEvent>(OnAccent);
            SubscribeLocalEvent<OwOAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
        }

        public string Accentuate(string message)
        {
            foreach (var (word, repl) in SpecialWords)
            {
                message = message.Replace(word, repl);
            }
            return message
                .Replace("r", "w").Replace("R", "W")
                .Replace("l", "w").Replace("L", "W");
        }

        private void OnAccent(Entity<OwOAccentComponent> entity, ref AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }

        private void OnAccentRelayed(Entity<OwOAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        {
            args.Args.Message = Accentuate(args.Args.Message);
        }

    }
}
