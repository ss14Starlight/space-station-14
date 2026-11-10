using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.MegaphoneShared;

public sealed class RMCMegaphoneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCMegaphoneComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<RMCMegaphoneComponent> ent, ref UseInHandEvent args)
    {
        args.Handled = true;
        var ev = new MegaphoneInputRequestedEvent(args.User);
        RaiseLocalEvent(ent.Owner, ev);
    }
}

public sealed class MegaphoneInputRequestedEvent(EntityUid user) : EntityEventArgs
{
    public EntityUid User = user;
}
