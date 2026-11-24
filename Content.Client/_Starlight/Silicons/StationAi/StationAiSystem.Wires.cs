using Content.Shared.Silicons.StationAi;
using Content.Shared.Wires;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private UserInterfaceSystem _userInterface = default!;

    private readonly ResPath _aiWiresRsi = new ResPath("/Textures/Interface/Actions/actions_borg.rsi");

    [SubscribeLocalEvent]
    private void OnWiresGetRadial(Entity<WiresPanelComponent> ent, ref GetStationAiRadialEvent args)
    {
        if (!TryComp<StationAiWhitelistComponent>(ent.Owner, out var whitelist)
            || !whitelist.Enabled
            || !_userInterface.HasUi(ent.Owner, WiresUiKey.Key)
            || (TryComp<WiresPanelSecurityComponent>(ent.Owner, out var panelSecurity)
                && !panelSecurity.WiresAccessible))
        {
            return;
        }

        args.Actions.Insert(0, new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiWiresRsi, "wire-module"),
            Tooltip = Loc.GetString("station-ai-access-wires"),
            Event = new StationAiOpenWiresPanelEvent(),
        });
    }
}
