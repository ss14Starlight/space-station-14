using Content.Shared._Starlight.Snapshots;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Snapshots;

/// <summary>
/// Sends a snapshot's <see cref="SnapshotData"/> to the one player viewing or holding it, and nobody else.
/// </summary>
public sealed partial class ServerSnapshotViewSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPlayerRateLimitManager _rateLimit = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;

    private const string RateLimitKey = "SnapshotView";

    public override void Initialize()
    {
        base.Initialize();

        _rateLimit.Register(RateLimitKey,
            new RateLimitRegistration(StarlightCCVars.SnapshotViewRateLimitPeriod,
                StarlightCCVars.SnapshotViewRateLimitCount,
                null));

        Subs.BuiEvents<SnapshotComponent>(SnapshotViewUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<SnapshotViewRequestMessage>(OnSnapshotRequested);
            });
    }

    /// <summary>
    /// Gives the holder the snapshot data on pickup so opening the view doesn't wait for the request roundtrip.
    /// Rate-limited like explicit requests; the first pickups still deliver.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPickedUp(Entity<SnapshotComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.Data is not { } data || !_actorQuery.TryComp(args.User, out var actor))
            return;

        if (_rateLimit.CountAction(actor.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
            return;

        RaiseNetworkEvent(new SnapshotViewDataEvent(GetNetEntity(ent), data), actor.PlayerSession);
    }

    /// <summary>
    /// Sends the snapshot to the one viewer who asked for it.
    /// </summary>
    private void OnSnapshotRequested(Entity<SnapshotComponent> ent, ref SnapshotViewRequestMessage args)
    {
        if (!_actorQuery.TryComp(args.Actor, out var actor)
            || _rateLimit.CountAction(actor.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            return;
        }

        SendSnapshot(ent, args.Actor);
    }

    /// <summary>
    /// Sends the snapshot right away so the view doesn't wait on the client's request.
    /// The request stays as a fallback for when this arrives before the client has the UI open.
    /// Rate-limited like the other paths; the first open still delivers.
    /// </summary>
    private void OnUiOpened(Entity<SnapshotComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!_actorQuery.TryComp(args.Actor, out var actor)
            || _rateLimit.CountAction(actor.PlayerSession, RateLimitKey) != RateLimitStatus.Allowed)
        {
            return;
        }

        SendSnapshot(ent, args.Actor);
    }

    /// <summary>
    /// Sends the snapshot to the viewer, closing the UI when there is nothing to show or the viewer is out of range.
    /// </summary>
    private void SendSnapshot(Entity<SnapshotComponent> ent, EntityUid viewer)
    {
        if (ent.Comp.Data is not { } data)
        {
            _ui.CloseUi(ent.Owner, SnapshotViewUiKey.Key, viewer);
            return;
        }

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
