using Content.Server._Starlight.Speech.Components;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class ChavAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChavAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ChavAccentComponent component, AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "chav");

        args.Message.Text = args.Message.Text
            .Replace("th", "ff")
            .Replace("Th", "Ff")
            .Replace("TH", "FF");
    }
}
