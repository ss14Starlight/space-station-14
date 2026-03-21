using System.Text;
using Content.Server.Speech.Components;
using Content.Server.Speech;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class SpanishAccentSystem : EntitySystem
{
    public override void Initialize() 
        => SubscribeLocalEvent<SpanishAccentComponent, AccentGetEvent>(OnAccent);

    public SpeechMessage Accentuate(SpeechMessage message)
    {
        // Insert E before every S
        message.Text = InsertS(message.Text);

        // If a sentence ends with ?, insert a reverse ? at the beginning
        message.Text = ReplacePunctuation(message.Text);

        return message;
    }

    private static string InsertS(string message)
    {
        var msg = message.Replace(" s", " es").Replace(" S", " Es");

        if (msg.StartsWith('s'))
            return msg[1..].Insert(0, "es");
        else if (msg.StartsWith('S'))
            return msg[1..].Insert(0, "Es");

        return msg;
    }

    private static string ReplacePunctuation(string message)
    {
        var sentences = AccentSystem.SentenceRegex.Split(message);
        var msg = new StringBuilder();
        foreach (var s in sentences)
        {
            var toInsert = new StringBuilder();
            for (var i = s.Length - 1; i >= 0 && "?!‽".Contains(s[i]); i--)
            {
                toInsert.Append(s[i] switch
                {
                    '?' => '¿',
                    '!' => '¡',
                    '‽' => '⸘',
                    _ => ' '
                });
            }
            if (toInsert.Length == 0)
                msg.Append(s);
            else
                msg.Append(s.Insert(s.Length - s.TrimStart().Length, toInsert.ToString()));
        }
        return msg.ToString();
    }

    private void OnAccent(EntityUid uid, SpanishAccentComponent component, AccentGetEvent args) 
        => args.Message = Accentuate(args.Message);
}
