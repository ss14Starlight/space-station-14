using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class ScottishAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"ing\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexIng();

    [GeneratedRegex(@"\band\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexAnd();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScottishAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ScottishAccentComponent component, AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "scottish");

        args.Message.Text = RegexIng().Replace(args.Message.Text, m => PreserveCase(m.Value, "in'"));
        args.Message.Text = RegexAnd().Replace(args.Message.Text, m => PreserveCase(m.Value, "an'"));
    }

    private static string PreserveCase(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original))
            return replacement;

        if (char.IsUpper(original[0]))
        {
            if (original.Length > 1 && char.IsUpper(original[1]))
                return replacement.ToUpperInvariant(); 
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        }

        return replacement; 
    }
}
