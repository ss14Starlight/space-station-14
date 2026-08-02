using Content.Client._Starlight.Scent.Overlays;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Scent.Systems;

// Also handles adding/removing ScentPerceptionOverlay for the local Smeller, so
// SharedScentSystem's handlers actually run on the client too.
public sealed class ClientScentSystem : SharedScentSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private ScentPerceptionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();

        SubscribeLocalEvent<SmellerComponent, ComponentInit>(OnSmellerInit);
        SubscribeLocalEvent<SmellerComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SmellerComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnSmellerInit(EntityUid uid, SmellerComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid && !_overlayMan.HasOverlay<ScentPerceptionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    // Overrides the base handler instead of subscribing to ComponentShutdown again: the base
    // class already owns that subscription, and SubscribeLocalEvent doesn't allow a second one
    // for the same (component, event) pair.
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
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}
