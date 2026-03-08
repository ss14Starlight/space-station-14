using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class SlowAccentSystem : EntitySystem
{
    [GeneratedRegex(@"\s|, |,")]
    private static partial Regex WordEndings();

    [GeneratedRegex(@"\w\z")]
    private static partial Regex NoFinalPunctuation();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlowAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<SlowAccentComponent> ent, ref AccentGetEvent args) 
        => args.Message.Text = Accentuate(args.Message.Text);

    public static string Accentuate(string message)
    {
        message = WordEndings().Replace(message, "... ");

        if (NoFinalPunctuation().IsMatch(message))
            message += "...";

        return message;
    }
}
