using System.Text;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class RussianAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize() 
        => SubscribeLocalEvent<RussianAccentComponent, AccentGetEvent>(OnAccent);

    public SpeechMessage Accentuate(SpeechMessage message)
    {
        message = _replacement.ApplyReplacements(message, "russian");

        // Visual cyrillic replacement (only for displayed text)
        var accentedMessage = new StringBuilder(message.Text);

        for (var i = 0; i < accentedMessage.Length; i++)
        {
            var c = accentedMessage[i];

            accentedMessage[i] = c switch
            {
                'A' => 'Д',
                'b' => 'в',
                'N' => 'И',
                'n' => 'и',
                'K' => 'К',
                'k' => 'к',
                'm' => 'м',
                'h' => 'н',
                't' => 'т',
                'R' => 'Я',
                'r' => 'я',
                'Y' => 'У',
                'W' => 'Ш',
                'w' => 'ш',
                _ => accentedMessage[i]
            };
        }

        message.Text = accentedMessage.ToString();
        return message;
    }

    private void OnAccent(EntityUid uid, RussianAccentComponent component, AccentGetEvent args) 
        => args.Message = Accentuate(args.Message);
}
