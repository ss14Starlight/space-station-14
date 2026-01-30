using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Paper;

public sealed class ChargesExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChargesExamineComponent,ExaminedEvent>(Examine);
    }

    private void Examine(EntityUid uid, ChargesExamineComponent component, ExaminedEvent args)
    {
        if (!TryComp<ActionsOnSignComponent>(args.Examined, out var actions))
            return;
        if (actions.Charges == 0)
            args.PushMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(component.LocNoCharges)));
        else
            args.PushMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(component.Loc, ("charges", actions.Charges))));
    }
}