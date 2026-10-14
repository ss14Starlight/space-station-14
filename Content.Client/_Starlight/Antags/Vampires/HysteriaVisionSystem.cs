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
public sealed partial class HysteriaVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<HysteriaVisionComponent> _hysteriaQuery;
    private HysteriaVisionOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _hysteriaQuery = GetEntityQuery<HysteriaVisionComponent>();

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

    private void OnHysteriaStartup(Entity<HysteriaVisionComponent> ent, ref ComponentStartup args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            AddOverlay();
    }

    private void OnHysteriaShutdown(Entity<HysteriaVisionComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<HysteriaVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => AddOverlay();

    private void OnPlayerDetached(Entity<HysteriaVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => RemoveOverlay();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check if we need to remove the overlay due to expiration
        var player = _playerManager.LocalEntity;
        if (player == null || !_hysteriaQuery.TryComp(player.Value, out var hysteria))
            return;

        // Server owns the component lifetime; client only hides the overlay while waiting for replication.
        if (_timing.CurTime > hysteria.EndTime)
            RemoveOverlay();
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
