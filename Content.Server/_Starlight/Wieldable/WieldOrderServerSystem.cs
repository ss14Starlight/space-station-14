using Content.Shared._Starlight.Wieldable;

namespace Content.Server._Starlight.Wieldable;

public sealed partial class WieldOrderServerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SetWieldOrderEvent>(OnSetWieldOrder);
    }

    private void OnSetWieldOrder(SetWieldOrderEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        var comp = EnsureComp<WieldOrderComponent>(player);
        comp.WieldBeforeRack = args.WieldBeforeRack;
        Dirty(player, comp);
    }
}
