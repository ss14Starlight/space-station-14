using Content.Client._Starlight.Scent.Overlays;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Scent.Systems;

/// <summary>
/// Also handles adding/removing ScentPerceptionOverlay for the local Smeller, so
/// SharedScentSystem's handlers actually run on the client too.
/// </summary>
public sealed partial class ClientScentSystem : SharedScentSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private ScentPerceptionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeLocalEvent<SmellerComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SmellerComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    /// <summary>
    /// ComponentShutdown/LocalPlayerDetachedEvent only fire on ordinary gameplay transitions, not
    /// when this system itself gets torn down (e.g. disconnecting mid-round). IOverlayManager is a
    /// process-lifetime singleton that outlives this system, so without this, _overlay stays
    /// registered forever, holding a live reference to this connection's entire entity graph.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    protected override void OnSmellerInit(EntityUid uid, SmellerComponent component, ComponentInit args)
    {
        base.OnSmellerInit(uid, component, args);

        if (_player.LocalEntity == uid && !_overlayMan.HasOverlay<ScentPerceptionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    /// Overrides the base handler instead of subscribing to ComponentShutdown again: the base
    /// class already owns that subscription, and SubscribeLocalEvent doesn't allow a second one
    /// for the same (component, event) pair.
    /// </summary>
    /// <param name="ent">The entity and SmellerComponent being removed.</param>
    /// <param name="args">Component shutdown event args.</param>
    protected override void OnSmellerShutdown(Entity<SmellerComponent> ent, ref ComponentShutdown args)
    {
        base.OnSmellerShutdown(ent, ref args);

        if (_player.LocalEntity == ent.Owner)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, SmellerComponent component, LocalPlayerAttachedEvent args)
    {
        if (!_overlayMan.HasOverlay<ScentPerceptionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, SmellerComponent component, LocalPlayerDetachedEvent args)
        => _overlayMan.RemoveOverlay(_overlay);
}
