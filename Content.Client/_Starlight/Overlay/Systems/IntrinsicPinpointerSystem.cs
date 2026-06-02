using Content.Client._Starlight.Overlay.Overlays;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Starlight.Overlay.Systems;

/// <summary>
/// Registers the <see cref="IntrinsicPinpointerOverlay"/> once at startup and keeps
/// it alive permanently. The overlay's own <c>BeforeDraw</c> checks each frame
/// whether the local player has the component, so no event subscriptions are needed.
/// This avoids all ghost-role-takeover event-ordering edge cases.
/// </summary>
public sealed class IntrinsicPinpointerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new IntrinsicPinpointerOverlay(EntityManager, _player, _eye));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<IntrinsicPinpointerOverlay>();
    }
}
