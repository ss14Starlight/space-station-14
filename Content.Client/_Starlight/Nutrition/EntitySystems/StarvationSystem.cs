using Content.Client._Starlight.Nutrition.Overlays;
using Content.Shared._Starlight.Nutrition.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Nutrition.EntitySystems;

public sealed partial class StarvationSystem : EntitySystem
{
    private readonly StarvationVignetteOverlay _vignette = new();
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize() => base.Initialize();

    [SubscribeLocalEvent]
    private void OnStarvationStartup(Entity<StarvationComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _overlayMan.AddOverlay(_vignette);
    }

    [SubscribeLocalEvent]
    private void OnStarvationShutdown(Entity<StarvationComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _overlayMan.RemoveOverlay(_vignette);
    }

    [SubscribeLocalEvent]
    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        if (!TryComp<StarvationComponent>(ev.Entity, out var hungry))
            return;

        _overlayMan.AddOverlay(_vignette);
    }

    [SubscribeLocalEvent]
    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        if (!TryComp<StarvationComponent>(ev.Entity, out var hungry))
            return;

        _overlayMan.RemoveOverlay(_vignette);
    }
}
