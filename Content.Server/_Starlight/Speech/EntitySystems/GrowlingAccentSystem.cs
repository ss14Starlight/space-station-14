using System.Text.RegularExpressions;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Speech.Components;

public sealed partial class GrowlingAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrowlingAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, GrowlingAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message.Text;

        // r => rrr
        message = Regexr().Replace(message, _random.Pick(new List<string> { "rr", "rrr" })
);
        // R => RRR
        message = RegexR().Replace(message, _random.Pick(new List<string> { "RR", "RRR" })
);

        args.Message.Text = message;
    }

    [GeneratedRegex("r+")]
    private static partial Regex Regexr();
    [GeneratedRegex("R+")]
    private static partial Regex RegexR();
}
