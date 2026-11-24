using Content.Shared._Starlight.UserInterface.Setup;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private readonly ResPath _setupIcon = new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png");

    [SubscribeLocalEvent]
    private void OnSetupGetRadial(Entity<SetupableComponent> ent, ref GetStationAiRadialEvent args)
    {
        if ((ent.Comp.NameSet && !ent.Comp.AllowRenaming)
            || !TryComp<StationAiWhitelistComponent>(ent.Owner, out var whitelist)
            || !whitelist.Enabled
            || !_userInterface.HasUi(ent.Owner, SetupUiKey.Key))
            return;

        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Texture(_setupIcon),
            Tooltip = Loc.GetString("setup-verb-text"),
            Event = new StationAiOpenSetupEvent()
        });
    }
}
