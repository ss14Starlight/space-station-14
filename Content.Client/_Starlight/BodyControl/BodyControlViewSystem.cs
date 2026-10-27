using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Starlight.BodyControl;

/// <summary>The entity displayed by the normal HUD, independently of the real player attachment.</summary>
public sealed partial class BodyControlViewSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    private EntityUid? _previous;
    private EntityUid? _previousLocal;
    private string? _previousHand;
    private bool _refreshPending = true;

    public EntityUid? Entity => TryGetSource(out _, out var source) ? source.Body : _players.LocalEntity;

    public bool TryGetSource(out EntityUid actor, out BodyControlSourceComponent source)
    {
        actor = _players.LocalEntity ?? EntityUid.Invalid;
        return TryComp(actor, out source!) && source.LifeStage <= ComponentLifeStage.Running
               && !source.ExternallyDriven && !TerminatingOrDeleted(source.Body);
    }

    public bool Send(BodyControlCommandEvent command)
    {
        if (!TryGetSource(out _, out _))
            return false;
        RaiseNetworkEvent(command);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnHandContext(Entity<HandsComponent> body, ref BodyControlHandContextEvent args)
    {
        if (TryGetSource(out var actor, out var source) && source.Body == body.Owner)
            args.Context = new(actor, source.Access, source.SelectedHand);
    }

    public void Refresh(EntityUid entity)
    {
        if (entity == _players.LocalEntity || entity == _previous)
            _refreshPending = true;
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(LocalPlayerAttachedEvent args) => _refreshPending = true;

    [SubscribeLocalEvent]
    private void OnPlayerDetached(LocalPlayerDetachedEvent args) => _refreshPending = true;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        // Rebuild after the complete state has arrived, not halfway through component replication.
        if (!_refreshPending)
            return;
        _refreshPending = false;
        var current = Entity;
        var local = _players.LocalEntity;
        var hand = TryGetSource(out _, out var source) ? source.SelectedHand : null;
        var localChanged = _previousLocal != local;
        var overridden = _previous != _previousLocal || current != local;
        var previous = _previous;
        _previous = current;
        _previousLocal = local;
        if ((previous != current || localChanged) && overridden)
            RaiseLocalEvent(new BodyControlViewChangedEvent(previous, current));
        else if (_previousHand != hand)
            RaiseLocalEvent(new BodyControlHandChangedEvent());
        _previousHand = hand;
    }
}

public sealed class BodyControlHandChangedEvent : EntityEventArgs;

public sealed class BodyControlViewChangedEvent(EntityUid? previous, EntityUid? current) : EntityEventArgs
{
    public readonly EntityUid? Previous = previous;
    public readonly EntityUid? Current = current;
}
