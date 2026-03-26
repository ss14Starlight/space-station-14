using Content.Shared._Starlight.Antags.Vampires.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Antags.Vampires;

/// <summary>
/// Client system that manages the HysteriaVisionOverlay.
/// Adds/removes the overlay based on whether the local player has HysteriaVisionComponent.
/// </summary>
public sealed class HysteriaVisionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private HysteriaVisionOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HysteriaVisionComponent, ComponentStartup>(OnHysteriaStartup);
        SubscribeLocalEvent<HysteriaVisionComponent, ComponentShutdown>(OnHysteriaShutdown);
        SubscribeLocalEvent<HysteriaVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<HysteriaVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnHysteriaStartup(EntityUid uid, HysteriaVisionComponent component, ComponentStartup args)
    {
        if (_playerManager.LocalEntity == uid)
            AddOverlay();
    }

    private void OnHysteriaShutdown(EntityUid uid, HysteriaVisionComponent component, ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == uid)
            RemoveOverlay();
    }

    private void OnPlayerAttached(EntityUid uid, HysteriaVisionComponent component, LocalPlayerAttachedEvent args)
        => AddOverlay();

    private void OnPlayerDetached(EntityUid uid, HysteriaVisionComponent component, LocalPlayerDetachedEvent args)
        => RemoveOverlay();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check if we need to remove the overlay due to expiration
        var player = _playerManager.LocalEntity;
        if (player == null || !TryComp<HysteriaVisionComponent>(player, out var hysteria))
            return;

        // Remove component if expired
        if (_timing.CurTime > hysteria.EndTime)
            RemComp<HysteriaVisionComponent>(player.Value);
    }

    private void AddOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new HysteriaVisionOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
