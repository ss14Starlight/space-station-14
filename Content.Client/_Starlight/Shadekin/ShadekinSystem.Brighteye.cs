using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Alert.Components;

namespace Content.Client._Starlight.Shadekin;

public sealed partial class ShadekinSystem : SharedShadekinSystem
{
    [SubscribeLocalEvent]
    private void OnGetCounterAmount(Entity<BrighteyeComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.BrighteyeAlert != args.Alert)
            return;

        args.Amount = ent.Comp.Energy;
    }
}
