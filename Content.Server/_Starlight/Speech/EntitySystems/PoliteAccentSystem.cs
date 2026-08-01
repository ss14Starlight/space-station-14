using Content.Server._Starlight.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class PoliteAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoliteAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, PoliteAccentComponent component, AccentGetEvent args)
        => args.Message = _replacement.ApplyReplacements(args.Message, "polite");
}
