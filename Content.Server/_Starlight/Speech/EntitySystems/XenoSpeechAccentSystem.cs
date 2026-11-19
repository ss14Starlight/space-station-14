using System.Text.RegularExpressions;
using Content.Shared._Starlight.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class XenoSpeechAccentSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoSpeechAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, XenoSpeechAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message.Text;

        // s => x
        message = Regexs().Replace(message, "$1x");
        // S => X
        message = RegexS().Replace(message, "$1X");

        args.Message.Text = message;
    }

    [GeneratedRegex("(^|\\s)s")]
    private static partial Regex Regexs();
    [GeneratedRegex("(^|\\s)S")]
    private static partial Regex RegexS();
}
