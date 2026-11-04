using Content.Shared._Moffstation.Snapshots;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Robust.Shared.Player;

namespace Content.Server._Moffstation.Snapshots;

/// <summary>
/// Sends a snapshot's <see cref="SnapshotData"/> to the one player viewing or holding it, and nobody else.
/// </summary>
public sealed partial class ServerSnapshotViewSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SnapshotComponent>(SnapshotViewUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<SnapshotViewRequestMessage>(OnSnapshotRequested);
            });
    }

    /// <summary>
    /// Almost Instantly gives the Entity holding it a snapshot so tehy dont have to wait for server
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPickedUp(Entity<SnapshotComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.Data is not { } data || !_actorQuery.TryComp(args.User, out var actor))
            return;

        RaiseNetworkEvent(new SnapshotViewDataEvent(GetNetEntity(ent), data), actor.PlayerSession);
    }

    /// <summary>
    /// Sends the snapshot to the one viewer who asked for it.
    /// </summary>

    private void OnSnapshotRequested(Entity<SnapshotComponent> ent, ref SnapshotViewRequestMessage args)
    {
        SendSnapshot(ent, args.Actor);
    }

    /// <summary>
    /// Sends the snapshot right away so the view doesn't wait on the client's request
    /// the request stays as a fallback for when this arrives before the client has the UI open.
    /// </summary>
    private void OnUiOpened(Entity<SnapshotComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendSnapshot(ent, args.Actor);
    }

    private void SendSnapshot(Entity<SnapshotComponent> ent, EntityUid viewer)
    {
        if (ent.Comp.Data is not { } data)
            return;

        if (!_ui.IsUiOpen(ent.Owner, SnapshotViewUiKey.Key, viewer) || !_interaction.InRangeUnobstructed(viewer, ent.Owner))
        {
            _ui.CloseUi(ent.Owner, SnapshotViewUiKey.Key, viewer);
            return;
        }

        if (!_actorQuery.TryComp(viewer, out var actor))
            return;

        RaiseNetworkEvent(new SnapshotViewDataEvent(GetNetEntity(ent), data), actor.PlayerSession);
    }
}
