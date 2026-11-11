using Content.Client._Funkystation.Traits.Assorted;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Blimpuf.Traits.Assorted;

public sealed class SeizureOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;

    private SeizureOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new SeizureOverlay();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
        if (_player.LocalEntity != null)
            _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    private void OnAttached(LocalPlayerAttachedEvent args) => _overlays.AddOverlay(_overlay);
    private void OnDetached(LocalPlayerDetachedEvent args) => _overlays.RemoveOverlay(_overlay);
}
