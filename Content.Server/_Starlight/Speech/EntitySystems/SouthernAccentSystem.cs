using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class SouthernAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"ing\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexIng();

    [GeneratedRegex(@"\band\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexAnd();

    [GeneratedRegex(@"d've\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexDve();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SouthernAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, SouthernAccentComponent component, AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "southern");

        args.Message.Text = RegexIng().Replace(args.Message.Text, m => PreserveCase(m.Value, "in'"));
        args.Message.Text = RegexAnd().Replace(args.Message.Text, m => PreserveCase(m.Value, "an'"));
        args.Message.Text = RegexDve().Replace(args.Message.Text, m => PreserveCase(m.Value, "da"));
    }

    private static string PreserveCase(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original))
            return replacement;

        if (char.IsUpper(original[0]))
        {
            return original.Length > 1 && char.IsUpper(original[1])
                ? replacement.ToUpperInvariant()
                : char.ToUpperInvariant(replacement[0]) + replacement[1..];
        }

        return replacement;
    }
}
