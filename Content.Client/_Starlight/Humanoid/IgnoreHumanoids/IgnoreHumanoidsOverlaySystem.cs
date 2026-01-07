using Content.Shared.IgnoreHumanoids;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client.IgnoreHumanoids;

public sealed class IgnoreHumanoidsOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private IgnoreHumanoidsOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgnoreHumanoidsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<IgnoreHumanoidsComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _overlay = new(EntityManager);
    }

    private void OnInit(EntityUid uid, IgnoreHumanoidsComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnRemove(EntityUid uid, IgnoreHumanoidsComponent component, ComponentRemove args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.Reset();
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (HasComp<IgnoreHumanoidsComponent>(args.Entity))
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (HasComp<IgnoreHumanoidsComponent>(args.Entity))
        {
            _overlay.Reset();
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _overlay.Reset();
        _overlayMan.RemoveOverlay(_overlay);
    }
}
