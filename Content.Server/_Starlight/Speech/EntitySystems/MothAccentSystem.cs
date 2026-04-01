using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class MothAccentSystem : EntitySystem
{
    [GeneratedRegex("z{1,3}", RegexOptions.IgnoreCase)]
    private static partial Regex RegexBuzz();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args) =>
        // buzzz - extend z sounds
        args.Message.Text = RegexBuzz().Replace(args.Message.Text, m =>
            char.IsUpper(m.Value[0]) ? "ZZZ" : "zzz");
}
