using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Player;
using Content.Shared._Starlight.Humanoid.IgnoreHumanoids;

namespace Content.Client._Starlight.Humanoid.IgnoreHumanoids;

public sealed partial class IgnoreHumanoidsOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;

    private IgnoreHumanoidsOverlay _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _overlay = new(EntityManager);
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<IgnoreHumanoidsComponent> ent, ref ComponentInit args) =>
        UpdateOverlay(ent.Owner, ignoreHumanoids: true);

    [SubscribeLocalEvent]
    private void OnRemove(Entity<IgnoreHumanoidsComponent> ent, ref ComponentRemove args) =>
        UpdateOverlay(ent.Owner, ignoreHumanoids: false);

    private void OnPlayerAttached(LocalPlayerAttachedEvent args) =>
        UpdateOverlay(args.Entity);

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlay.Reset();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _overlay.Reset();
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is { } uid)
            UpdateOverlay(uid);
    }

    private void UpdateOverlay(EntityUid uid, bool? ignoreHumanoids = null)
    {
        if (_player.LocalEntity != uid)
            return;

        var shouldShow = ignoreHumanoids ?? HasComp<IgnoreHumanoidsComponent>(uid);

        if (shouldShow)
        {
            if (!_overlayMan.HasOverlay<IgnoreHumanoidsOverlay>())
                _overlayMan.AddOverlay(_overlay);

            return;
        }

        if (_overlayMan.HasOverlay<IgnoreHumanoidsOverlay>())
        {
            _overlay.Reset();
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}
