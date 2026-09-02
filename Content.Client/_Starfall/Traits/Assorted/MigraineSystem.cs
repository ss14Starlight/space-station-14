using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Starfall.Traits.Assorted;

/// <summary>
/// Manages client-side visuals for migraines
/// </summary>
public sealed partial class MigraineSystem : EntitySystem
{
    [Dependency]
    private IOverlayManager _overlayManager = null!;

    [Dependency]
    private IPlayerManager _playerManager = null!;

    private MigraineOverlay _overlay = null!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new MigraineOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);

        if (_playerManager.LocalEntity != null)
            _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

}
