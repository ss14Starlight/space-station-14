using Content.Shared.Actions;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Friendship;

public sealed partial class SharedFriendshipFamiliarSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, SummonFriendInstantEvent>(OnSummonFriend);
    }

    private void OnSummonFriend(Entity<ActorComponent> ent, ref SummonFriendInstantEvent args)
    {
        args.Handled = true;
    }
}

public sealed partial class SummonFriendInstantEvent : InstantActionEvent;